using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Components.Map;

namespace Genelife.Map;

/// <summary>
/// Derived static map index. It mirrors <see cref="Placement"/> components,
/// owns occupancy and lot reservations, and can be rebuilt from the world.
/// It is never serialized as authoritative state.
/// </summary>
public sealed class WorldMap
{
    private readonly Dictionary<NetworkId, Placement> placements = new();
    private readonly Dictionary<TileCoord, NetworkId> occupiedBy = new();
    private readonly Dictionary<LotId, MapLot> lots = new();
    private readonly HashSet<TileCoord> reserved = new();

    public MapOptions Options { get; private set; } = MapOptions.Default;
    public GeneratedMap? Layout { get; private set; }
    public int Version { get; private set; } = 1;

    public IReadOnlyDictionary<NetworkId, Placement> Placements => placements;
    public IReadOnlyCollection<MapLot> Lots => lots.Values;
    public int OccupiedTileCount => occupiedBy.Count;

    public int ReservedTileCount => reserved.Count;

    public void Load(GeneratedMap layout, int version)
    {
        Layout = layout;
        Options = layout.Options;
        Version = Math.Max(1, version);
        placements.Clear();
        occupiedBy.Clear();
        reserved.Clear();
        lots.Clear();

        foreach (var lot in layout.Lots)
            lots[lot.Id] = lot;
    }

    public void ResetGeneration(GeneratedMap layout)
    {
        Load(layout, 1);
    }

    public bool IsInBounds(TileCoord tile) => Options.Bounds.Contains(tile);

    public bool IsWalkable(TileCoord tile)
    {
        if (!IsInBounds(tile))
            return false;

        var flags = FlagsAt(tile);
        return (flags & (TileFlags.Land | TileFlags.Road)) != 0
            && (flags & (TileFlags.Water | TileFlags.Occupied | TileFlags.Reserved)) == 0;
    }

    public bool IsRoad(TileCoord tile) =>
        IsInBounds(tile) && (FlagsAt(tile) & TileFlags.Road) != 0;

    /// <summary>
    /// The flags of a tile: base terrain comes from the lazy <see cref="GeneratedMap.Tiles"/>
    /// grid (Rule 1 — classified on demand), while <see cref="TileFlags.Occupied"/> and
    /// <see cref="TileFlags.Reserved"/> are sparse overlays tracked by this map. Nothing is
    /// stored per tile, so the world never materialises an <c>O(area)</c> array.
    /// </summary>
    public TileFlags FlagsAt(TileCoord tile)
    {
        if (!IsInBounds(tile))
            return TileFlags.None;

        var flags = Layout?.FlagsAt(tile) ?? TileFlags.None;
        if (occupiedBy.ContainsKey(tile))
            flags |= TileFlags.Occupied;
        if (reserved.Contains(tile))
            flags |= TileFlags.Reserved;
        return flags;
    }

    public bool TryGetPlacement(NetworkId id, out Placement placement) =>
        placements.TryGetValue(id, out placement);

    public NetworkId BuildingAt(TileCoord tile) =>
        occupiedBy.TryGetValue(tile, out var id) ? id : default;

    public IEnumerable<NetworkId> BuildingsInBounds(MapBounds bounds)
    {
        foreach (var pair in placements)
        {
            if (pair.Value.Bounds.Intersects(bounds))
                yield return pair.Key;
        }
    }

    public PlacementError CanPlace(
        BuildingKind kind,
        Footprint footprint,
        TileCoord anchor,
        string district,
        out MapLot? lot,
        out string? message)
    {
        lot = null;
        message = null;

        if (!footprint.IsValid)
        {
            message = "Footprint must be positive.";
            return PlacementError.InvalidFootprint;
        }

        var bounds = MapBounds.FromAnchor(anchor, footprint);
        if (!Options.Bounds.Contains(bounds))
        {
            message = $"Footprint {bounds} is outside {Options.Bounds}.";
            return PlacementError.OutOfBounds;
        }

        var preferredZone = ZoneFor(kind);
        lot = FindLot(bounds, preferredZone);
        if (lot is null)
        {
            message = $"No {preferredZone} lot can fit {footprint} at {anchor}.";
            return PlacementError.NoAvailableLot;
        }

        if (lot.Zone != preferredZone && lot.Zone != MapZone.Mixed)
        {
            message = $"Lot {lot.Id} is zoned {lot.Zone}, not {preferredZone}.";
            return PlacementError.WrongZone;
        }

        if (lot.IsReserved && lot.ReservedBy != default)
        {
            message = $"Lot {lot.Id} is reserved by building {lot.ReservedBy}.";
            return PlacementError.LotReserved;
        }

        foreach (var tile in footprint.Tiles(anchor))
        {
            var flags = FlagsAt(tile);
            if ((flags & (TileFlags.Water | TileFlags.Occupied)) != 0)
            {
                message = $"Tile {tile} is already blocked.";
                return PlacementError.Occupied;
            }
        }

        if (!HasRoadAccess(footprint, anchor))
        {
            message = "Building has no adjacent road access.";
            return PlacementError.NoRoadAccess;
        }

        return PlacementError.None;
    }

    public Placement Place(
        NetworkId id,
        BuildingKind kind,
        Footprint footprint,
        TileCoord anchor,
        TileCoord entry,
        LotId? lotId = null)
    {
        if (placements.ContainsKey(id))
            throw new InvalidOperationException($"Building {id.Value} already has a placement.");

        var placement = new Placement(anchor, footprint, entry, kind);
        AddPlacement(id, placement);
        if (lotId is { } lotKey)
            ReleaseLot(lotKey);
        Version++;
        return placement;
    }

    /// <summary>
    /// Validates a move for an already placed building. Tiles already occupied by
    /// the building are ignored so a building can move within or over its own
    /// footprint without a transient double-reservation.
    /// </summary>
    public PlacementError CanRelocate(
        NetworkId id,
        TileCoord anchor,
        TileCoord entry,
        out MapLot? lot,
        out string? message)
    {
        lot = null;
        message = null;

        if (!placements.TryGetValue(id, out var current))
        {
            message = $"Building {id.Value} has no placement.";
            return PlacementError.DuplicateNetworkId;
        }

        var footprint = current.Footprint;
        var kind = current.Kind;
        var bounds = MapBounds.FromAnchor(anchor, footprint);
        if (!Options.Bounds.Contains(bounds))
        {
            message = $"Footprint {bounds} is outside {Options.Bounds}.";
            return PlacementError.OutOfBounds;
        }

        var preferredZone = ZoneFor(kind);
        lot = FindLot(bounds, preferredZone);
        if (lot is null)
        {
            message = $"No {preferredZone} lot can fit {footprint} at {anchor}.";
            return PlacementError.NoAvailableLot;
        }

        var reservedBySelf = lot.IsReserved && lot.ReservedBy == id.Value;
        if (lot.IsReserved && lot.ReservedBy != default && !reservedBySelf)
        {
            message = $"Lot {lot.Id} is reserved by building {lot.ReservedBy}.";
            return PlacementError.LotReserved;
        }

        var blockedFlags = reservedBySelf ? TileFlags.Water : TileFlags.Water | TileFlags.Reserved;
        foreach (var tile in footprint.Tiles(anchor))
        {
            if (occupiedBy.TryGetValue(tile, out var owner) && owner != id)
            {
                message = $"Tile {tile} is occupied by building {owner.Value}.";
                return PlacementError.Occupied;
            }

            var flags = FlagsAt(tile);
            if ((flags & blockedFlags) != 0)
            {
                message = $"Tile {tile} is blocked.";
                return PlacementError.Occupied;
            }
        }

        if (!HasRoadAccess(footprint, anchor))
        {
            message = "Building has no adjacent road access.";
            return PlacementError.NoRoadAccess;
        }

        if (footprint.Tiles(anchor).Contains(entry))
        {
            message = "Entry must be outside the destination footprint.";
            return PlacementError.EntryBlocked;
        }

        var entryIsOwnTile = occupiedBy.TryGetValue(entry, out var entryOwner) && entryOwner == id;
        var entryBlockedByTerrain = (FlagsAt(entry) & (TileFlags.Water | TileFlags.Reserved)) != 0;
        if (entryBlockedByTerrain || (!entryIsOwnTile && !IsWalkable(entry)))
        {
            message = $"Entry {entry} is not walkable.";
            return PlacementError.EntryBlocked;
        }

        return PlacementError.None;
    }

    /// <summary>
    /// Applies a relocation that already passed <see cref="CanRelocate"/>.
    /// Occupancy moves atomically and the map version advances.
    /// </summary>
    public Placement Relocate(NetworkId id, TileCoord anchor, TileCoord entry)
    {
        if (!placements.TryGetValue(id, out var current))
            throw new InvalidOperationException($"Building {id.Value} has no placement.");

        RemoveInternal(id);
        var placement = new Placement(anchor, current.Footprint, entry, current.Kind, current.Rotation);
        AddPlacement(id, placement);
        ReleaseLotReservedBy(id, current.Bounds, current.Kind);
        ReleaseLotReservedBy(id, placement.Bounds, placement.Kind);
        Version++;
        return placement;
    }

    private void ReleaseLotReservedBy(NetworkId id, MapBounds bounds, BuildingKind kind)
    {
        var lot = FindLot(bounds, ZoneFor(kind));
        if (lot is { IsReserved: true } && lot.ReservedBy == id.Value)
            ReleaseLot(lot.Id);
    }

    public void RestorePlacement(NetworkId id, Placement placement)
    {
        AddPlacement(id, placement);
    }

    public bool Remove(NetworkId id)
    {        if (!placements.Remove(id, out var placement))
            return false;

        foreach (var tile in placement.Tiles())
            occupiedBy.Remove(tile);

        Version++;
        return true;
    }

    public bool TryReserveLot(LotId lotId, NetworkId buildingId, out MapLot? lot)
    {
        lot = null;
        if (!lots.TryGetValue(lotId, out var found) || found.IsReserved)
            return false;

        lot = found;
        found.IsReserved = true;
        found.ReservedBy = buildingId.Value;
        foreach (var tile in Enumerate(found.Bounds))
            SetReserved(tile, true);
        return true;
    }

    public void ReleaseLot(LotId lotId)
    {
        if (!lots.TryGetValue(lotId, out var lot))
            return;

        lot.IsReserved = false;
        lot.ReservedBy = null;
        foreach (var tile in Enumerate(lot.Bounds))
        {
            if (!occupiedBy.ContainsKey(tile))
                SetReserved(tile, false);
        }
    }

    public MapLot? FindAvailableLot(BuildingKind kind, Footprint footprint)
    {
        var preferred = ZoneFor(kind);
        var ordered = lots.Values
            .Where(lot => !lot.IsReserved && lot.CanFit(footprint))
            .OrderBy(lot => lot.Zone == preferred ? 0 : lot.Zone == MapZone.Mixed ? 1 : 2)
            .ThenBy(lot => lot.Id.Value)
            .ToList();

        foreach (var lot in ordered)
        {
            var anchor = new TileCoord(lot.Bounds.MinX, lot.Bounds.MinY);
            if (CanPlace(kind, footprint, anchor, string.Empty, out _, out _) == PlacementError.None)
                return lot;
        }

        return null;
    }

    public PlacementError TryChooseAnchor(
        BuildingKind kind,
        Footprint footprint,
        string district,
        out TileCoord anchor,
        out MapLot? lot,
        out string? message)
    {
        var preferred = ZoneFor(kind);
        foreach (var candidate in lots.Values
                     .Where(item => !item.IsReserved && item.CanFit(footprint))
                     .OrderBy(item => item.Zone == preferred ? 0 : item.Zone == MapZone.Mixed ? 1 : 2)
                     .ThenBy(item => item.Id.Value))
        {
            var candidateAnchor = new TileCoord(candidate.Bounds.MinX, candidate.Bounds.MinY);
            var error = CanPlace(kind, footprint, candidateAnchor, district, out lot, out message);
            if (error == PlacementError.None)
            {
                anchor = candidateAnchor;
                return error;
            }
        }

        anchor = default;
        lot = null;
        message = $"No available lot can fit {footprint} for {kind}.";
        return PlacementError.NoAvailableLot;
    }

    public List<PlacementIssue> Reconcile(World world)
    {
        var issues = new List<PlacementIssue>();
        var seen = new HashSet<NetworkId>();
        var occupied = new Dictionary<TileCoord, NetworkId>();
        var query = new QueryDescription().WithAll<Placement, NetworkId>();
        world.Query(in query, (Entity entity, ref Placement placement, ref NetworkId id) =>
        {
            if (!seen.Add(id))
                issues.Add(new PlacementIssue(id.Value, "DuplicateNetworkId", "Multiple placements share a network id.", placement.Anchor));

            if (!Options.Bounds.Contains(placement.Bounds))
                issues.Add(new PlacementIssue(id.Value, "OutOfBounds", "Placement lies outside map bounds.", placement.Anchor));

            foreach (var tile in placement.Tiles())
            {
                if (occupied.TryGetValue(tile, out var other))
                    issues.Add(new PlacementIssue(id.Value, "Overlap", $"Tile {tile} is also occupied by {other.Value}.", tile));
                else
                    occupied[tile] = id;
            }
        });
        return issues;
    }

    public void Rebuild(World world)
    {
        foreach (var id in placements.Keys.ToList())
            RemoveInternal(id);

        placements.Clear();
        occupiedBy.Clear();
        foreach (var lot in lots.Values)
        {
            lot.IsReserved = false;
            lot.ReservedBy = null;
        }

        reserved.Clear();

        var query = new QueryDescription().WithAll<Placement, NetworkId>();
        world.Query(in query, (ref Placement placement, ref NetworkId id) =>
        {
            AddPlacement(id, placement);
        });
    }

    private void AddPlacement(NetworkId id, Placement placement)
    {
        placements[id] = placement;
        foreach (var tile in placement.Tiles())
        {
            occupiedBy[tile] = id;
            SetReserved(tile, false);
        }
    }

    private void RemoveInternal(NetworkId id)
    {
        if (!placements.TryGetValue(id, out var placement))
            return;

        foreach (var tile in placement.Tiles())
            occupiedBy.Remove(tile);
    }

    /// <summary>
    /// Sets or clears the sparse <see cref="TileFlags.Reserved"/> overlay on a tile.
    /// Out-of-bounds tiles are ignored, matching the historical dense-flag guard.
    /// </summary>
    private void SetReserved(TileCoord tile, bool enabled)
    {
        if (!IsInBounds(tile))
            return;

        if (enabled)
            reserved.Add(tile);
        else
            reserved.Remove(tile);
    }

    private bool HasRoadAccess(Footprint footprint, TileCoord anchor)
    {
        for (var x = -1; x <= footprint.Width; x++)
        {
            if (IsRoad(new TileCoord(anchor.X + x, anchor.Y - 1)) ||
                IsRoad(new TileCoord(anchor.X + x, anchor.Y + footprint.Height)))
                return true;
        }

        for (var y = -1; y <= footprint.Height; y++)
        {
            if (IsRoad(new TileCoord(anchor.X - 1, anchor.Y + y)) ||
                IsRoad(new TileCoord(anchor.X + footprint.Width, anchor.Y + y)))
                return true;
        }

        return false;
    }

    public TileCoord ChooseEntry(Footprint footprint, TileCoord anchor)
    {
        var candidates = new List<TileCoord>();
        for (var x = -1; x <= footprint.Width; x++)
        {
            candidates.Add(new TileCoord(anchor.X + x, anchor.Y - 1));
            candidates.Add(new TileCoord(anchor.X + x, anchor.Y + footprint.Height));
        }
        for (var y = -1; y <= footprint.Height; y++)
        {
            candidates.Add(new TileCoord(anchor.X - 1, anchor.Y + y));
            candidates.Add(new TileCoord(anchor.X + footprint.Width, anchor.Y + y));
        }

        foreach (var tile in candidates.OrderBy(candidate => IsRoad(candidate) ? 0 : 1).ThenBy(candidate => candidate.Y).ThenBy(candidate => candidate.X))
        {
            if (IsWalkable(tile))
                return tile;
        }

        return anchor;
    }

    private MapLot? FindLot(MapBounds bounds, MapZone preferred)
    {
        return lots.Values
            .Where(lot => lot.Bounds.Contains(bounds) && (lot.Zone == preferred || lot.Zone == MapZone.Mixed))
            .OrderBy(lot => lot.Id.Value)
            .FirstOrDefault();
    }

    public static MapZone ZoneFor(BuildingKind kind) => kind switch
    {
        BuildingKind.Residential => MapZone.Residential,
        BuildingKind.Office => MapZone.Commercial,
        BuildingKind.School => MapZone.Education,
        BuildingKind.Civic => MapZone.Civic,
        _ => MapZone.Mixed
    };

    private static IEnumerable<TileCoord> Enumerate(MapBounds bounds)
    {
        for (var y = bounds.MinY; y <= bounds.MaxY; y++)
        {
            for (var x = bounds.MinX; x <= bounds.MaxX; x++)
                yield return new TileCoord(x, y);
        }
    }
}

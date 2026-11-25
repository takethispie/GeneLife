using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Components.Map;

namespace Genelife.Map;

/// <summary>
/// The single validated path for attaching a building placement. It updates the
/// derived <see cref="WorldMap"/> and never performs I/O or event writes.
/// </summary>
public sealed class BuildingPlacementService
{
    private readonly World world;
    private readonly WorldMap map;

    public BuildingPlacementService(World world, WorldMap map)
    {
        this.world = world;
        this.map = map;
    }

    public PlacementResult TryPlace(
        Entity building,
        NetworkId buildingId,
        BuildingKind kind,
        Footprint footprint,
        string district,
        TileCoord? desiredAnchor = null,
        TileCoord? desiredEntry = null)
    {
        if (map.TryGetPlacement(buildingId, out _))
            return PlacementResult.Fail(PlacementError.DuplicateNetworkId, $"Building {buildingId.Value} already has a placement.");

        TileCoord anchor;
        MapLot? lot;
        string? message;

        if (desiredAnchor is { } requested)
        {
            var error = map.CanPlace(kind, footprint, requested, district, out lot, out message);
            if (error != PlacementError.None)
                return PlacementResult.Fail(error, message);
            anchor = requested;
        }
        else
        {
            var error = map.TryChooseAnchor(kind, footprint, district, out anchor, out lot, out message);
            if (error != PlacementError.None)
                return PlacementResult.Fail(error, message);
        }

        var entry = desiredEntry ?? map.ChooseEntry(footprint, anchor);
        if (!map.IsWalkable(entry))
        {
            if (desiredEntry is not null)
                return PlacementResult.Fail(PlacementError.EntryBlocked, $"Entry {entry} is not walkable.");

            entry = map.ChooseEntry(footprint, anchor);
            if (!map.IsWalkable(entry))
                return PlacementResult.Fail(PlacementError.EntryBlocked, "No walkable entry is adjacent to the footprint.");
        }

        if (world.Has<Placement>(building))
            world.Remove<Placement>(building);

        var placement = map.Place(buildingId, kind, footprint, anchor, entry, lot?.Id);
        world.Add(building, placement);
        if (!world.Has<NetworkId>(building))
            world.Add(building, buildingId);

        return PlacementResult.Ok(placement, lot?.Id);
    }

    public PlacementResult PlaceInZone(
        Entity building,
        NetworkId buildingId,
        BuildingKind kind,
        Footprint footprint,
        string district) =>
        TryPlace(building, buildingId, kind, footprint, district);

    public PlacementResult RestorePlacement(Entity building, NetworkId buildingId, Placement placement)
    {
        if (map.TryGetPlacement(buildingId, out _))
            return PlacementResult.Fail(PlacementError.DuplicateNetworkId, $"Building {buildingId.Value} already has a placement.");

        if (!map.IsInBounds(placement.Anchor) || !map.IsInBounds(placement.Entry))
            return PlacementResult.Fail(PlacementError.OutOfBounds, $"Placement {placement.Anchor} is outside the map.");

        foreach (var tile in placement.Tiles())
        {
            if (map.BuildingAt(tile) != default)
                return PlacementResult.Fail(PlacementError.Occupied, $"Tile {tile} is already occupied.");
        }

        map.RestorePlacement(buildingId, placement);
        if (world.Has<Placement>(building))
            world.Get<Placement>(building) = placement;
        else
            world.Add(building, placement);
        if (!world.Has<NetworkId>(building))
            world.Add(building, buildingId);

        return PlacementResult.Ok(placement);
    }

    public bool Remove(NetworkId buildingId)
    {
        return map.Remove(buildingId);
    }

    public bool TryReserveLot(LotId lotId, NetworkId buildingId, out MapLot? lot) =>
        map.TryReserveLot(lotId, buildingId, out lot);

    public void ReleaseLot(LotId lotId) => map.ReleaseLot(lotId);

    public List<PlacementIssue> Validate() => map.Reconcile(world);
}

public sealed class MapContext
{
    public MapContext(
        World world,
        MapOptions options,
        MapGenerationOptions generationOptions,
        GeneratedMap generated,
        WorldMap map)
    {
        World = world;
        Options = options;
        GenerationOptions = generationOptions;
        Generated = generated;
        Map = map;
        Random = new SimulationRandom(options.Seed);
        Placement = new BuildingPlacementService(world, map);
    }

    public World World { get; }
    public MapOptions Options { get; private set; }
    public MapGenerationOptions GenerationOptions { get; }
    public GeneratedMap Generated { get; private set; }
    public WorldMap Map { get; }
    public BuildingPlacementService Placement { get; }
    public SimulationRandom Random { get; }
    public int Version => Map.Version;

    public void LoadRestored(GeneratedMap generated, int version)
    {
        Options = generated.Options;
        Generated = generated;
        Map.Load(generated, version);
    }

    public static MapContext Generate(
        World world,
        MapOptions? options = null,
        MapGenerationOptions? generationOptions = null)
    {
        options ??= MapOptions.Default;
        generationOptions ??= MapGenerationOptions.Default;
        var generated = new MapGenerator().Generate(options, generationOptions);
        var map = new WorldMap();
        map.Load(generated, options.Version);
        return new MapContext(world, options, generationOptions, generated, map);
    }
}

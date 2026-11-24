using Arch.Core;
using Genelife.Components.Company;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Components.Map;
using Genelife.Components.Social;
using Genelife.Economy;
using Genelife.Map;
using Genelife.Messages.Events;
using Genelife.Player;

namespace Genelife.Social;

/// <summary>
/// Venue configuration, admission and occupancy. Presence is derived from the
/// controlled sim's tile each tick; capacity and opening hours are enforced
/// before a player becomes an occupant and gains the venue channel.
/// </summary>
public sealed class VenueService
{
    private readonly object gate = new();
    private readonly World world;
    private readonly PlayerRegistry players;
    private readonly EconomyContext economy;
    private readonly VenueOptions options;
    private readonly Func<string, bool>? isOnline;
    private readonly Dictionary<Entity, HashSet<string>> occupants = new();
    private readonly Dictionary<string, Entity> venueByPlayer = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Entity> removedFrom = new(StringComparer.Ordinal);
    private readonly Dictionary<(string PlayerId, Entity Venue), long> lastRejection = new();
    private long lastConfiguredTick = -1;

    public VenueService(
        World world,
        PlayerRegistry players,
        EconomyContext economy,
        VenueOptions? options = null,
        Func<string, bool>? isOnline = null)
    {
        this.world = world;
        this.players = players;
        this.economy = economy;
        this.options = options ?? new VenueOptions();
        this.isOnline = isOnline;
        this.options.Validate();
    }

    public VenueOptions Options => options;

    public void EnsureConfiguredVenues()
    {
        var candidates = new List<(Entity Entity, Placement Placement)>();
        world.Query(in new QueryDescription().WithAll<Placement>(),
            (Entity entity, ref Placement placement) =>
            {
                if (!world.Has<SocialVenue>(entity))
                    candidates.Add((entity, placement));
            });

        foreach (var (entity, placement) in candidates)
        {
            if (!world.IsAlive(entity))
                continue;

            SocialVenueKind? kind = null;
            if (world.Has<Office>(entity))
                kind = SocialVenueKind.OfficeLobby;
            else if (world.Has<School>(entity))
                kind = SocialVenueKind.SchoolYard;
            else if (world.Has<Property>(entity) && world.Get<Property>(entity).IsShelter)
                kind = SocialVenueKind.Civic;
            else if (placement.Kind == BuildingKind.Store)
                kind = SocialVenueKind.Cafe;
            else if (placement.Kind == BuildingKind.Civic)
                kind = SocialVenueKind.Park;

            if (kind is { } venueKind)
                world.Add(entity, options.DefaultsFor(venueKind));
        }
    }

    public Entity FindByNetworkId(ulong networkId)
    {
        var found = Entity.Null;
        var query = new QueryDescription().WithAll<SocialVenue, NetworkId>();
        world.Query(in query, (Entity entity, ref NetworkId id) =>
        {
            if (found == Entity.Null && id.Value == networkId)
                found = entity;
        });
        return found;
    }

    public Entity VenueAt(TileCoord tile)
    {
        var found = Entity.Null;
        var query = new QueryDescription().WithAll<Placement, SocialVenue>();
        world.Query(in query, (Entity entity, ref Placement placement) =>
        {
            if (found != Entity.Null)
                return;
            if (placement.Covers(tile) || placement.Entry == tile)
                found = entity;
        });
        return found;
    }

    public Entity VenueOf(string playerId)
    {
        lock (gate)
            return venueByPlayer.TryGetValue(playerId, out var venue) ? venue : Entity.Null;
    }

    public bool IsOccupant(string playerId, Entity venue)
    {
        lock (gate)
            return occupants.TryGetValue(venue, out var set) && set.Contains(playerId);
    }

    public IReadOnlyList<string> Occupants(Entity venue)
    {
        lock (gate)
            return occupants.TryGetValue(venue, out var set)
                ? set.OrderBy(id => id, StringComparer.Ordinal).ToArray()
                : Array.Empty<string>();
    }

    public int Occupancy(Entity venue)
    {
        lock (gate)
            return occupants.TryGetValue(venue, out var set) ? set.Count : 0;
    }

    public bool IsOpen(Entity venue, long tick)
    {
        if (!world.IsAlive(venue) || !world.Has<SocialVenue>(venue))
            return false;

        var data = world.Get<SocialVenue>(venue);
        if (data.Closed)
            return false;

        var day = PositiveModulo(tick, options.TicksPerDay);
        var from = PositiveModulo(data.OpenFromTick, options.TicksPerDay);
        var to = PositiveModulo(data.OpenToTick, options.TicksPerDay);
        return from <= to
            ? day >= from && day < to
            : day >= from || day < to;
    }

    public bool TryEnter(
        string playerId,
        Entity venue,
        long tick,
        out string code,
        out string message)
    {
        code = string.Empty;
        message = string.Empty;
        if (!world.IsAlive(venue) || !world.Has<SocialVenue>(venue) || !world.Has<Placement>(venue))
        {
            code = "venue_not_found";
            message = "That venue is not available.";
            return false;
        }

        var data = world.Get<SocialVenue>(venue);
        if (data.Closed)
        {
            code = "venue_closed";
            message = "The venue is closed.";
            return false;
        }
        if (!IsOpen(venue, tick))
        {
            code = "venue_closed";
            message = "The venue is outside its opening hours.";
            return false;
        }

        Entity previous = Entity.Null;
        lock (gate)
        {
            if (removedFrom.TryGetValue(playerId, out var blockedVenue) && blockedVenue == venue)
            {
                code = "removed_from_venue";
                message = "A moderator removed you from this venue.";
                return false;
            }

            if (venueByPlayer.TryGetValue(playerId, out var current) && current == venue)
                return true;

            var set = occupants.TryGetValue(venue, out var existing)
                ? existing
                : occupants[venue] = new HashSet<string>(StringComparer.Ordinal);
            if (set.Count >= data.Capacity)
            {
                code = "venue_full";
                message = "The venue is at capacity.";
                return false;
            }

            previous = venueByPlayer.TryGetValue(playerId, out var previousVenue)
                ? previousVenue
                : Entity.Null;
            LeaveLocked(playerId, tick);
            set.Add(playerId);
            venueByPlayer[playerId] = venue;
        }

        if (previous != Entity.Null && previous != venue)
            economy.Emit(new VenueLeft(NetworkIdOf(previous), NetworkIdOfPlayer(playerId), tick));
        economy.Emit(new VenueEntered(NetworkIdOf(venue), NetworkIdOfPlayer(playerId), tick));
        return true;
    }

    public bool Leave(string playerId, long tick)
    {
        bool removed;
        Entity venue;
        lock (gate)
        {
            venue = venueByPlayer.TryGetValue(playerId, out var current) ? current : Entity.Null;
            removed = LeaveLocked(playerId, tick);
        }

        if (removed && venue != Entity.Null)
            economy.Emit(new VenueLeft(NetworkIdOf(venue), NetworkIdOfPlayer(playerId), tick));
        return removed;
    }

    public void RefreshPresence(long tick)
    {
        if (lastConfiguredTick < 0 || tick - lastConfiguredTick >= options.ConfigureRefreshTicks)
        {
            EnsureConfiguredVenues();
            lastConfiguredTick = tick;
        }

        foreach (var profile in players.All().ToArray())
        {
            if (isOnline is not null && !isOnline(profile.PlayerId))
            {
                Leave(profile.PlayerId, tick);
                continue;
            }

            if (profile.ControlledSim == Entity.Null ||
                !world.IsAlive(profile.ControlledSim) ||
                world.Has<Components.Gen.Dead>(profile.ControlledSim) ||
                !world.Has<TilePosition>(profile.ControlledSim))
            {
                Leave(profile.PlayerId, tick);
                continue;
            }

            var tile = world.Get<TilePosition>(profile.ControlledSim).Tile;
            var venue = VenueAt(tile);
            var current = VenueOf(profile.PlayerId);

            if (removedFrom.TryGetValue(profile.PlayerId, out var blockedVenue))
            {
                if (blockedVenue != venue)
                    removedFrom.Remove(profile.PlayerId);
            }

            if (venue == current)
            {
                if (venue != Entity.Null && !IsOpen(venue, tick))
                    Leave(profile.PlayerId, tick);
                continue;
            }

            if (venue != Entity.Null)
            {
                if (!TryEnter(profile.PlayerId, venue, tick, out var code, out _))
                {
                    Leave(profile.PlayerId, tick);
                    EmitRejection(profile.PlayerId, venue, code, tick);
                }
            }
            else
            {
                Leave(profile.PlayerId, tick);
            }
        }
    }

    public bool SetClosed(Entity venue, bool closed, long tick)
    {
        if (!world.IsAlive(venue) || !world.Has<SocialVenue>(venue))
            return false;

        var data = world.Get<SocialVenue>(venue);
        data.Closed = closed;
        world.Get<SocialVenue>(venue) = data;
        economy.Emit(new VenueClosed(NetworkIdOf(venue), closed, tick));

        if (!closed)
            return true;

        string[] removed;
        lock (gate)
        {
            removed = occupants.TryGetValue(venue, out var set) ? set.ToArray() : Array.Empty<string>();
            foreach (var playerId in removed)
                LeaveLocked(playerId, tick);
        }

        foreach (var playerId in removed)
            economy.Emit(new VenueLeft(NetworkIdOf(venue), NetworkIdOfPlayer(playerId), tick));

        return true;
    }

    public bool RemoveOccupant(string playerId, Entity venue, long tick)
    {
        lock (gate)
        {
            if (!occupants.TryGetValue(venue, out var set) || !set.Remove(playerId))
                return false;
            venueByPlayer.Remove(playerId);
        }

        lock (gate)
            removedFrom[playerId] = venue;

        economy.Emit(new VenueLeft(NetworkIdOf(venue), NetworkIdOfPlayer(playerId), tick));
        economy.Emit(new VenueOccupantRemoved(NetworkIdOf(venue), playerId, tick));
        return true;
    }

    private bool LeaveLocked(string playerId, long tick)
    {
        if (!venueByPlayer.TryGetValue(playerId, out var venue))
            return false;

        venueByPlayer.Remove(playerId);
        if (occupants.TryGetValue(venue, out var set))
        {
            set.Remove(playerId);
            if (set.Count == 0)
                occupants.Remove(venue);
        }

        return true;
    }

    private void EmitRejection(string playerId, Entity venue, string code, long tick)
    {
        var key = (playerId, venue);
        lock (gate)
        {
            if (lastRejection.TryGetValue(key, out var last)
                && tick - last < options.RejectionNoticeCooldownTicks)
                return;
            lastRejection[key] = tick;
        }

        economy.Emit(new VenueEntryRejected(NetworkIdOf(venue), playerId, code, tick));
    }

    private ulong NetworkIdOfPlayer(string playerId) =>
        players.TryGet(playerId, out var profile) && profile.ControlledSim != Entity.Null
            ? NetworkIdOf(profile.ControlledSim)
            : 0;

    private ulong NetworkIdOf(Entity entity) =>
        world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0;

    private static long PositiveModulo(long value, int modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}

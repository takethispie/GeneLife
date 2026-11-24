using Arch.Core;
using Genelife.Api.Services;
using Genelife.Api.Social;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Map;
using Genelife.Components.Player;
using Genelife.Components.Social;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Map;
using Genelife.Messages.Events;
using Genelife.Player;
using Genelife.Social;

namespace Genelife.UnitTests.Social;

public sealed class VenueTests : IDisposable
{
    private readonly World world = World.Create();
    private readonly EconomyContext economy;
    private readonly NetworkIdRegistry networkIds = new();
    private readonly PlayerRegistry players = new();
    private readonly SocialGraphService social = new();
    private readonly VenueOptions options = new()
    {
        ActivityCooldownTicks = 5,
        RecentActivityTicks = 100
    };
    private readonly VenueService venues;
    private readonly VenueActivityService activities;
    private readonly Entity venue;

    public VenueTests()
    {
        economy = new EconomyContext(world, clock: SimulationClock.Default);
        venues = new VenueService(world, players, economy, options);
        activities = new VenueActivityService(world, economy, players, venues, social, options);
        venue = CreateVenue(
            SocialVenueKind.Cafe,
            capacity: 2,
            openFrom: 7,
            openTo: 22,
            VenueActivityMask.Sit | VenueActivityMask.Talk
                | VenueActivityMask.ShareMeal | VenueActivityMask.Cheer);
    }

    [Fact]
    public void EnteringAndLeavingControlsVenuePresence()
    {
        var alice = CreatePlayer("alice", "Alice", 100, 9, 10);
        var bob = CreatePlayer("bob", "Bob", 200, 9, 10);

        venues.RefreshPresence(tick: 8);

        Assert.True(venues.IsOccupant("alice", venue));
        Assert.True(venues.IsOccupant("bob", venue));
        Assert.Equal(new[] { "alice", "bob" }, venues.Occupants(venue));

        world.Get<TilePosition>(alice) = new TilePosition(new TileCoord(0, 0));
        venues.RefreshPresence(tick: 9);

        Assert.False(venues.IsOccupant("alice", venue));
        Assert.True(venues.IsOccupant("bob", venue));
        Assert.DoesNotContain("alice", venues.Occupants(venue));
    }

    [Fact]
    public void CapacityAndOpeningHoursRejectEntry()
    {
        CreatePlayer("alice", "Alice", 100, 9, 10);
        CreatePlayer("bob", "Bob", 200, 9, 10);
        var venueData = world.Get<SocialVenue>(venue);
        venueData.Capacity = 1;
        world.Get<SocialVenue>(venue) = venueData;

        Assert.True(venues.TryEnter("alice", venue, tick: 8, out _, out _));
        venues.RefreshPresence(tick: 8);
        Assert.True(venues.IsOccupant("alice", venue));
        Assert.False(venues.IsOccupant("bob", venue));

        var full = venues.TryEnter("bob", venue, tick: 8, out var fullCode, out _);
        Assert.False(full);
        Assert.Equal("venue_full", fullCode);
        Assert.Contains(economy.PendingEvents.OfType<VenueEntryRejected>(),
            entry => entry.PlayerId == "bob" && entry.Code == "venue_full");

        Assert.True(venues.SetClosed(venue, true, tick: 9));
        var closed = venues.TryEnter("carol", venue, tick: 8, out var closedCode, out _);
        Assert.False(closed);
        Assert.Equal("venue_closed", closedCode);

        Assert.True(venues.SetClosed(venue, false, tick: 10));
        var afterHours = venues.TryEnter("carol", venue, tick: 23, out var hoursCode, out _);
        Assert.False(afterHours);
        Assert.Equal("venue_closed", hoursCode);
    }

    [Fact]
    public void ModeratorRemovalKeepsPlayerOutUntilTheyLeaveTheFootprint()
    {
        CreatePlayer("alice", "Alice", 100, 9, 10);
        venues.RefreshPresence(tick: 8);
        Assert.True(venues.IsOccupant("alice", venue));

        Assert.True(venues.RemoveOccupant("alice", venue, tick: 9));
        Assert.False(venues.IsOccupant("alice", venue));

        venues.RefreshPresence(tick: 10);
        Assert.False(venues.IsOccupant("alice", venue));
        Assert.False(venues.TryEnter("alice", venue, tick: 10, out var code, out _));
        Assert.Equal("removed_from_venue", code);

        world.Get<TilePosition>(players.GetOrCreate("alice", Entity.Null, 0).ControlledSim) =
            new TilePosition(new TileCoord(0, 0));
        venues.RefreshPresence(tick: 11);
        Assert.False(venues.IsOccupant("alice", venue));

        world.Get<TilePosition>(players.GetOrCreate("alice", Entity.Null, 0).ControlledSim) =
            new TilePosition(new TileCoord(9, 10));
        venues.RefreshPresence(tick: 12);
        Assert.True(venues.IsOccupant("alice", venue));
    }

    [Fact]
    public void ActivitiesRespectMaskCooldownAndConsumeRealFood()
    {
        var alice = CreatePlayer("alice", "Alice", 100, 9, 10);
        var bob = CreatePlayer("bob", "Bob", 200, 9, 10);
        var pantry = AddPantry(alice, units: 1);
        venues.RefreshPresence(tick: 8);

        var notAllowed = activities.TryPerform("alice", venue, VenueActivity.PlayGame, tick: 8);
        Assert.False(notAllowed.Applied);
        Assert.Equal("activity_not_allowed", notAllowed.Code);

        var sit = activities.TryPerform("alice", venue, VenueActivity.Sit, tick: 8);
        Assert.True(sit.Applied, sit.Code);
        Assert.Contains("bob", sit.Recipients);
        Assert.Contains(economy.PendingEvents.OfType<VenueActivityPerformed>(),
            performed => performed.Activity == nameof(VenueActivity.Sit));

        var cooldown = activities.TryPerform("alice", venue, VenueActivity.ShareMeal, tick: 9);
        Assert.False(cooldown.Applied);
        Assert.Equal("activity_cooldown", cooldown.Code);

        var meal = activities.TryPerform("alice", venue, VenueActivity.ShareMeal, tick: 14);
        Assert.True(meal.Applied, meal.Code);
        Assert.Equal(0, world.Get<HouseholdPantry>(pantry).FoodUnits);

        var noFood = activities.TryPerform("alice", venue, VenueActivity.ShareMeal, tick: 20);
        Assert.False(noFood.Applied);
        Assert.Equal("no_food", noFood.Code);
    }

    [Fact]
    public async Task VenueChatReachesOnlyOccupantsAndBlockedPairsStaySilent()
    {
        CreatePlayer("alice", "Alice", 100, 9, 10);
        CreatePlayer("bob", "Bob", 200, 9, 10);
        CreatePlayer("carol", "Carol", 300, 0, 0);
        venues.RefreshPresence(tick: 8);

        var directory = new VenueChatDirectory(world, venues, players);
        var chat = new ChatService(
            new InMemoryChatStore(),
            social,
            directory,
            new ChatOptions(),
            TimeProvider.System);

        var sent = await chat.SendAsync("alice", new ChatSendRequest
        {
            Text = "hello venue",
            Channel = ChatChannel.Venue
        }, tick: 8);

        Assert.True(sent.Accepted, sent.Code);
        Assert.Equal(new[] { "bob" }, sent.Recipients);
        Assert.DoesNotContain("carol", sent.Recipients);

        Assert.True(social.Block("bob", "alice", tick: 9).Accepted);
        var blocked = await chat.SendAsync("alice", new ChatSendRequest
        {
            Text = "still here",
            Channel = ChatChannel.Venue
        }, tick: 9);

        Assert.True(blocked.Accepted, blocked.Code);
        Assert.Empty(blocked.Recipients);

        venues.Leave("alice", tick: 10);
        var left = await chat.SendAsync("alice", new ChatSendRequest
        {
            Text = "outside",
            Channel = ChatChannel.Venue
        }, tick: 10);

        Assert.False(left.Accepted);
        Assert.Equal("not_in_venue", left.Code);
    }

    private Entity CreateVenue(
        SocialVenueKind kind,
        int capacity,
        long openFrom,
        long openTo,
        VenueActivityMask mask)
    {
        var entity = world.Create(new SocialVenue
        {
            Kind = kind,
            Capacity = capacity,
            OpenFromTick = openFrom,
            OpenToTick = openTo,
            Activities = mask
        });
        world.Add(entity, new Placement(
            new TileCoord(10, 10),
            new Footprint(2, 2),
            new TileCoord(9, 10),
            BuildingKind.Civic));
        world.Add(entity, new NetworkId(900));
        networkIds.AllocateWithValue(entity, new NetworkId(900));
        return entity;
    }

    private Entity CreatePlayer(string playerId, string name, ulong networkId, int x, int y)
    {
        var sim = world.Create(
            new GenName(name),
            new PlayerControlled(playerId),
            new TilePosition(new TileCoord(x, y)),
            new Needs());
        world.Add(sim, new NetworkId(networkId));
        networkIds.AllocateWithValue(sim, new NetworkId(networkId));
        players.GetOrCreate(playerId, sim, 0);
        return sim;
    }

    private Entity AddPantry(Entity sim, int units)
    {
        var household = world.Create(new Household { Head = sim, MemberCount = 1 });
        world.Add(household, new HouseholdPantry { FoodUnits = units });
        world.Add(sim, new HouseholdMember(household, HouseholdRole.Head));
        economy.Housing.AddMember(sim, household);
        return household;
    }

    private sealed class VenueChatDirectory : IChatDirectory
    {
        private readonly World world;
        private readonly VenueService venues;
        private readonly PlayerRegistry players;

        public VenueChatDirectory(World world, VenueService venues, PlayerRegistry players)
        {
            this.world = world;
            this.venues = venues;
            this.players = players;
        }

        public ChatPlayerContext? Describe(string playerId)
        {
            if (!players.TryGet(playerId, out var profile) ||
                profile.ControlledSim == Entity.Null ||
                !world.IsAlive(profile.ControlledSim))
                return null;

            var sim = profile.ControlledSim;
            var tile = world.Has<TilePosition>(sim) ? world.Get<TilePosition>(sim).Tile : default;
            var venue = venues.VenueOf(playerId);
            var inVenue = venue != Entity.Null && venues.IsOccupant(playerId, venue);
            return new ChatPlayerContext(
                playerId,
                world.Has<GenName>(sim) ? world.Get<GenName>(sim).Name : null,
                tile.X,
                tile.Y,
                inVenue ? NetworkIdOf(venue) : null,
                inVenue);
        }

        public IReadOnlyList<ChatPlayerContext> Online() =>
            players.All()
                .Select(profile => Describe(profile.PlayerId))
                .Where(context => context is not null)
                .Cast<ChatPlayerContext>()
                .ToArray();

        private ulong NetworkIdOf(Entity entity) =>
            world.IsAlive(entity) && world.Has<NetworkId>(entity)
                ? world.Get<NetworkId>(entity).Value
                : 0;
    }

    public void Dispose() => world.Dispose();
}

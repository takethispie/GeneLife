using Arch.Core;
using Genelife.Api.Auth;
using Genelife.Api.Services;
using Genelife.Api.Social;
using Genelife.Components.Gen;
using Genelife.Components.Social;
using Genelife.Components.Map;
using Genelife.Map;
using Genelife.Social;

namespace Genelife.UnitTests.Social;

public class PresenceTests
{
    [Fact]
    public void NearbyDirectoryIsBoundedPrivacySafeAndReconnectSafe()
    {
        var connections = new ConnectionRegistry();
        var privacy = new SocialPrivacyRegistry();
        using var engine = new SimulationEngine();
        engine.Initialize();
        var p1 = engine.EnsurePlayerGenos("alice");
        var p2 = engine.EnsurePlayerGenos("bob");
        var p3 = engine.EnsurePlayerGenos("carol");
        Assert.NotEqual(0UL, p1);
        Assert.NotEqual(0UL, p2);
        Assert.NotEqual(0UL, p3);

        Entity venue = default;
        engine.ExecuteWithWorld(world =>
        {
            Place(world, engine, p1, new TileCoord(10, 10));
            Place(world, engine, p2, new TileCoord(12, 10));
            Place(world, engine, p3, new TileCoord(30, 30));

            venue = world.Create(
                new Placement(new TileCoord(8, 8), new Footprint(10, 10), new TileCoord(10, 10), BuildingKind.Store),
                new SocialVenue
                {
                    Kind = SocialVenueKind.Cafe,
                    Capacity = 4,
                    OpenFromTick = 0,
                    OpenToTick = 23,
                    Activities = VenueActivityMask.Talk
                },
                new NetworkId(900));
            engine.NetworkIds.AllocateWithValue(venue, new NetworkId(900));
            return 0;
        });
        Assert.True(engine.Venues!.TryEnter("alice", venue, tick: 8, out _, out _));
        Assert.True(engine.Venues.TryEnter("bob", venue, tick: 8, out _, out _));

        connections.Register("c1", "alice");
        connections.Register("c2", "bob");
        connections.Register("c3", "carol");
        var directory = new PlayerDirectoryService(engine, connections, privacy, new SocialGraphService(), new SocialRateLimitOptions { PresencePerPlayer = 1000, PresenceGlobal = 1000 });

        var alicePresence = engine.BuildPlayerPresence("alice");
        Assert.NotNull(alicePresence);
        Assert.NotNull(alicePresence!.BuildingNetworkId);
        var bobPresence = engine.BuildPlayerPresence("bob");
        Assert.True(bobPresence?.BuildingNetworkId == alicePresence.BuildingNetworkId,
            $"alice={alicePresence.BuildingNetworkId} bob={bobPresence?.BuildingNetworkId} bobTile={bobPresence?.TileX},{bobPresence?.TileY}");
        var nearby = directory.Directory("alice", PresenceScope.Nearby, radius: 4);
        Assert.Equal("Nearby", nearby.Scope);
        Assert.Contains(nearby.Items, item => item.PlayerId == "bob");
        Assert.DoesNotContain(nearby.Items, item => item.PlayerId == "carol");

        var venueScope = directory.Directory("alice", PresenceScope.Venue, radius: 4);
        Assert.Contains(venueScope.Items, item => item.PlayerId == "bob");
        Assert.DoesNotContain(venueScope.Items, item => item.PlayerId == "carol");

        privacy.Set("bob", new SocialPrivacy { ShowDisplayName = false, AllowNearbyForNonFriends = true });
        var hidden = directory.Directory("alice", PresenceScope.Nearby, radius: 4);
        Assert.Equal("Resident", hidden.Items.Single(item => item.PlayerId == "bob").DisplayName);

        privacy.Set("bob", new SocialPrivacy { ShowDisplayName = true, AllowNearbyForNonFriends = false });
        Assert.DoesNotContain(
            directory.Directory("alice", PresenceScope.Nearby, radius: 4).Items,
            item => item.PlayerId == "bob");

        connections.TryUnregister("c2", out _);
        Assert.DoesNotContain(
            directory.Directory("alice", PresenceScope.Nearby, radius: 4).Items,
            item => item.PlayerId == "bob");

        privacy.Set("bob", new SocialPrivacy { ShowDisplayName = true, AllowNearbyForNonFriends = true });
        connections.Register("c4", "bob");
        Assert.Contains(
            directory.Directory("alice", PresenceScope.Nearby, radius: 4).Items,
            item => item.PlayerId == "bob");

        var fields = typeof(PlayerPresenceDto).GetProperties().Select(property => property.Name).ToHashSet();
        Assert.DoesNotContain("Wallet", fields);
        Assert.DoesNotContain("Health", fields);
        Assert.DoesNotContain("Traits", fields);
        Assert.DoesNotContain("Family", fields);
        Assert.DoesNotContain("Household", fields);
    }

    [Fact]
    public void FriendScopeAndBlocksFilterPresenceInBothDirections()
    {
        var connections = new ConnectionRegistry();
        using var engine = new SimulationEngine();
        engine.Initialize();
        var aliceGenos = engine.EnsurePlayerGenos("alice");
        var bobGenos = engine.EnsurePlayerGenos("bob");
        engine.ExecuteWithWorld(world =>
        {
            Place(world, engine, aliceGenos, new TileCoord(10, 10));
            Place(world, engine, bobGenos, new TileCoord(11, 10));
            return 0;
        });

        connections.Register("c1", "alice");
        connections.Register("c2", "bob");
        var graph = new SocialGraphService();
        var directory = new PlayerDirectoryService(engine, connections, new SocialPrivacyRegistry(), graph, new SocialRateLimitOptions { PresencePerPlayer = 1000, PresenceGlobal = 1000 });

        Assert.Empty(directory.Directory("alice", PresenceScope.Friend).Items);

        Assert.True(graph.AddFriend("alice", "bob", tick: 1).Accepted);
        Assert.Contains(
            directory.Directory("alice", PresenceScope.Friend).Items,
            item => item.PlayerId == "bob");
        Assert.Contains(
            directory.Directory("bob", PresenceScope.Friend).Items,
            item => item.PlayerId == "alice");

        Assert.True(graph.Block("alice", "bob", tick: 2, SocialEdgeSource.Player).Accepted);
        Assert.DoesNotContain(
            directory.Directory("alice", PresenceScope.Nearby, radius: 12).Items,
            item => item.PlayerId == "bob");
        Assert.DoesNotContain(
            directory.Directory("bob", PresenceScope.Nearby, radius: 12).Items,
            item => item.PlayerId == "alice");
    }

    [Fact]
    public void PresenceQueriesAreRateLimitedPerPlayer()
    {
        var connections = new ConnectionRegistry();
        using var engine = new SimulationEngine();
        engine.Initialize();
        var alice = engine.EnsurePlayerGenos("alice");
        var bob = engine.EnsurePlayerGenos("bob");
        engine.ExecuteWithWorld(world =>
        {
            Place(world, engine, alice, new TileCoord(10, 10));
            Place(world, engine, bob, new TileCoord(11, 10));
            return 0;
        });
        connections.Register("c1", "alice");
        connections.Register("c2", "bob");

        var time = new MutableTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var directory = new PlayerDirectoryService(
            engine,
            connections,
            new SocialPrivacyRegistry(),
            new SocialGraphService(),
            new SocialRateLimitOptions
            {
                PresencePerPlayer = 1,
                PresenceGlobal = 100,
                PresenceWindowSeconds = 10
            },
            time);

        var first = directory.Directory("alice", PresenceScope.Nearby, radius: 4);
        Assert.Contains(first.Items, item => item.PlayerId == "bob");

        var limited = directory.Directory("alice", PresenceScope.Nearby, radius: 4);
        Assert.Empty(limited.Items);
        Assert.NotNull(limited.Notice);

        time.Advance(TimeSpan.FromSeconds(11));
        var recovered = directory.Directory("alice", PresenceScope.Nearby, radius: 4);
        Assert.Contains(recovered.Items, item => item.PlayerId == "bob");
    }

    private static void Place(World world, SimulationEngine engine, ulong genosNetworkId, TileCoord tile)
    {
        Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(genosNetworkId), out var genos));
        if (world.Has<TilePosition>(genos))
            world.Get<TilePosition>(genos) = new TilePosition(tile);
        else
            world.Add(genos, new TilePosition(tile));
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset current = now;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan by) => current += by;
    }
}

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
        var p1 = engine.EnsurePlayerSim("alice");
        var p2 = engine.EnsurePlayerSim("bob");
        var p3 = engine.EnsurePlayerSim("carol");
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
        var directory = new PlayerDirectoryService(engine, connections, privacy, new SocialGraphService());

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
        var aliceSim = engine.EnsurePlayerSim("alice");
        var bobSim = engine.EnsurePlayerSim("bob");
        engine.ExecuteWithWorld(world =>
        {
            Place(world, engine, aliceSim, new TileCoord(10, 10));
            Place(world, engine, bobSim, new TileCoord(11, 10));
            return 0;
        });

        connections.Register("c1", "alice");
        connections.Register("c2", "bob");
        var graph = new SocialGraphService();
        var directory = new PlayerDirectoryService(engine, connections, new SocialPrivacyRegistry(), graph);

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

    private static void Place(World world, SimulationEngine engine, ulong simNetworkId, TileCoord tile)
    {
        Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(simNetworkId), out var sim));
        if (world.Has<TilePosition>(sim))
            world.Get<TilePosition>(sim) = new TilePosition(tile);
        else
            world.Add(sim, new TilePosition(tile));
    }
}

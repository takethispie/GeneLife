using System.Text.Json;
using Genelife.Api.Auth;
using Genelife.Api.Persistence;
using Genelife.Api.Services;
using Genelife.Api.Social;
using Genelife.Components.Gen;
using Genelife.Components.Map;
using Genelife.Map;
using Genelife.Social;

namespace Genelife.UnitTests.Social;

/// <summary>
/// Cross-cutting feature 04 acceptance: presence lifecycle, public presence
/// shape, schema migration and the no-private-identifiers rule.
/// </summary>
public sealed class SocialAcceptanceTests : IDisposable
{
    private readonly SimulationEngine engine = new();
    private readonly SocialGraphService social = new();
    private readonly SocialPrivacyRegistry privacy = new();
    private readonly ConnectionRegistry connections = new();
    private readonly PlayerDirectoryService directory;

    public SocialAcceptanceTests()
    {
        engine.Initialize();
        directory = new PlayerDirectoryService(
            engine,
            connections,
            privacy,
            social,
            new SocialRateLimitOptions { PresencePerPlayer = 1000, PresenceGlobal = 1000 });
    }

    [Fact]
    public void PresenceVisibilityFollowsProximityDisconnectAndReconnect()
    {
        var aliceId = engine.EnsurePlayerSim("alice");
        var bobId = engine.EnsurePlayerSim("bob");
        engine.ExecuteWithWorld(world =>
        {
            Place(world, aliceId, new TileCoord(10, 10));
            Place(world, bobId, new TileCoord(11, 10));
            return 0;
        });
        connections.Register("alice-1", "alice");
        connections.Register("bob-1", "bob");

        Assert.Contains(directory.Directory("alice", PresenceScope.Nearby).Items,
            item => item.PlayerId == "bob");

        engine.ExecuteWithWorld(world =>
        {
            Place(world, bobId, new TileCoord(30, 30));
            return 0;
        });
        Assert.DoesNotContain(directory.Directory("alice", PresenceScope.Nearby).Items,
            item => item.PlayerId == "bob");

        engine.ExecuteWithWorld(world =>
        {
            Place(world, bobId, new TileCoord(11, 10));
            return 0;
        });
        connections.TryUnregister("bob-1", out _);
        Assert.DoesNotContain(directory.Directory("alice", PresenceScope.Nearby).Items,
            item => item.PlayerId == "bob");

        connections.Register("bob-2", "bob");
        Assert.Contains(directory.Directory("alice", PresenceScope.Nearby).Items,
            item => item.PlayerId == "bob");
    }

    [Fact]
    public void PlayerFacingPresenceContainsOnlyPublicFields()
    {
        var publicNames = typeof(PlayerPresenceDto)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(publicNames, name => name.Contains("Wallet", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicNames, name => name.Contains("Needs", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicNames, name => name.Contains("Trait", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicNames, name => name.Contains("Health", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicNames, name => name.Contains("Email", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicNames, name => name.Contains("Ip", StringComparison.OrdinalIgnoreCase));

        var presence = new PublicPresence(
            "alice",
            "Alice",
            100,
            1,
            2,
            900,
            PresenceState.InVenue,
            42);
        var json = JsonSerializer.Serialize(presence);
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ipAddress", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("wallet", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PersistedSocialStateContainsNoRealNameEmailOrIpFields()
    {
        var state = new PersistedWorldState();
        var json = JsonSerializer.Serialize(state);
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ipAddress", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("realName", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("chat", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Schema14FixtureMigratesAndRestoresWithDefaults()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "v14-social.json");
        var state = JsonSerializer.Deserialize<PersistedWorldState>(File.ReadAllText(path))!;

        SnapshotMigrations.Upgrade(state, 14);

        Assert.Equal(PersistedWorldState.CurrentSchemaVersion, state.SchemaVersion);
        Assert.Empty(state.SocialEdges);
        Assert.Empty(state.SocialRequests);
        var setting = Assert.Single(state.SocialSettings);
        Assert.Equal("fixture-player", setting.PlayerId);
        Assert.True(setting.ShowDisplayName);

        using var restored = new SimulationEngine();
        restored.Restore(state, restored.NetworkIds);
        Assert.Empty(restored.SocialGraph.AdminEdges());
        restored.Shutdown();
    }

    private void Place(Arch.Core.World world, ulong simNetworkId, TileCoord tile)
    {
        if (!engine.NetworkIds.TryGetEntity(new NetworkId(simNetworkId), out var sim))
            throw new InvalidOperationException($"Missing sim {simNetworkId}");
        world.Get<TilePosition>(sim) = new TilePosition(tile);
    }

    public void Dispose()
    {
        engine.Shutdown();
        engine.Dispose();
    }
}

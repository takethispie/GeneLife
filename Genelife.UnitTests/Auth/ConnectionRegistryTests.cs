using Genelife.Api.Auth;

namespace Genelife.UnitTests.Auth;

public sealed class ConnectionRegistryTests
{
    private readonly ConnectionRegistry registry = new();

    [Fact]
    public void Register_maps_connection_to_player()
    {
        registry.Register("connection-1", "player-1");

        Assert.True(registry.TryGetPlayerId("connection-1", out var playerId));
        Assert.Equal("player-1", playerId);
    }

    [Fact]
    public void Register_replaces_existing_mapping_for_connection()
    {
        registry.Register("connection-1", "player-1");
        registry.Register("connection-1", "player-2");

        Assert.True(registry.TryGetPlayerId("connection-1", out var playerId));
        Assert.Equal("player-2", playerId);
        Assert.Empty(registry.GetConnections("player-1"));
    }

    [Fact]
    public void GetConnections_returns_all_connections_for_player()
    {
        registry.Register("connection-1", "player-1");
        registry.Register("connection-2", "player-1");
        registry.Register("connection-3", "player-2");

        var connections = registry.GetConnections("player-1");

        Assert.Equal(2, connections.Count);
        Assert.Contains("connection-1", connections);
        Assert.Contains("connection-2", connections);
    }

    [Fact]
    public void GetPlayerIds_returns_distinct_players()
    {
        registry.Register("connection-1", "player-1");
        registry.Register("connection-2", "player-1");
        registry.Register("connection-3", "player-2");

        var playerIds = registry.GetPlayerIds();

        Assert.Equal(2, playerIds.Count);
        Assert.Contains("player-1", playerIds);
        Assert.Contains("player-2", playerIds);
    }

    [Fact]
    public void TryUnregister_removes_mapping()
    {
        registry.Register("connection-1", "player-1");

        Assert.True(registry.TryUnregister("connection-1", out var playerId));
        Assert.Equal("player-1", playerId);
        Assert.False(registry.TryGetPlayerId("connection-1", out _));
    }

    [Fact]
    public void TryUnregister_returns_false_for_unknown_connection()
    {
        Assert.False(registry.TryUnregister("missing", out var playerId));
        Assert.Null(playerId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_rejects_blank_values(string value)
    {
        Assert.Throws<ArgumentException>(() => registry.Register(value, "player-1"));
        Assert.Throws<ArgumentException>(() => registry.Register("connection-1", value));
    }
}

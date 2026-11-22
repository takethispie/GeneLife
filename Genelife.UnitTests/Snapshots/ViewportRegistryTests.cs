using Genelife.Api.Snapshots;

namespace Genelife.UnitTests.Snapshots;

public class ViewportRegistryTests
{
    [Fact]
    public void Connection_viewport_can_be_set_and_read()
    {
        var registry = new ViewportRegistry();

        Assert.True(registry.TrySet("connection-1", new Viewport(1, 2, 3)));
        Assert.True(registry.TryGet("connection-1", out var viewport));
        Assert.Equal(new Viewport(1, 2, 3), viewport);
    }

    [Fact]
    public void Player_viewport_survives_a_connection_disconnect()
    {
        var registry = new ViewportRegistry();
        registry.SetForPlayer("player-1", new Viewport(4, 5, 6));
        registry.TrySet("connection-1", new Viewport(4, 5, 6));

        registry.Remove("connection-1");

        Assert.False(registry.TryGet("connection-1", out _));
        Assert.True(registry.TryGetForPlayer("player-1", out var restored));
        Assert.Equal(new Viewport(4, 5, 6), restored);
    }

    [Fact]
    public void Clear_player_viewport_removes_restore_state()
    {
        var registry = new ViewportRegistry();
        registry.SetForPlayer("player-1", new Viewport(0, 0, 8));

        registry.RemovePlayer("player-1");

        Assert.False(registry.TryGetForPlayer("player-1", out _));
    }

    [Fact]
    public void Viewport_uses_the_exit_margin_for_hysteresis()
    {
        var viewport = new Viewport(0, 0, 5) { ExitMargin = 2 };

        Assert.True(viewport.Contains(5, 5));
        Assert.False(viewport.Contains(6, 0));
        Assert.True(viewport.ContainsWithMargin(6, 0));
        Assert.True(viewport.ContainsWithMargin(7, 0));
        Assert.False(viewport.ContainsWithMargin(8, 0));
    }
}

using Genelife.Api.DTOs;
using Genelife.Api.Snapshots;

namespace Genelife.UnitTests.Snapshots;

public class AoiViewportDeltaTests
{
    private static WorldSnapshot Snapshot(long tick, params SimStatus[] sims) =>
        new(tick, 2, sims.ToList(), new List<CompanyStatus>());

    private static SimStatus Sim(ulong id, int x, int y) => new()
    {
        NetworkId = id,
        Name = $"Sim_{id}",
        TileX = x,
        TileY = y,
        X = x,
        Y = y
    };

    [Fact]
    public void BuildViewportDelta_Includes_sim_that_entered_the_viewport()
    {
        var previous = Snapshot(1, Sim(1, 100, 100));
        var current = Snapshot(2, Sim(1, 2, 2));

        var delta = AoiFilter.BuildViewportDelta(previous, current, new Viewport(0, 0, 5));

        Assert.False(delta.Full);
        Assert.Contains(delta.Sims, sim => sim.NetworkId == 1);
        Assert.Empty(delta.RemovedSims);
    }

    [Fact]
    public void BuildViewportDelta_Removes_sim_that_left_the_viewport()
    {
        var previous = Snapshot(1, Sim(1, 2, 2));
        var current = Snapshot(2, Sim(1, 100, 100));

        var delta = AoiFilter.BuildViewportDelta(previous, current, new Viewport(0, 0, 5));

        Assert.Empty(delta.Sims);
        Assert.Contains(1UL, delta.RemovedSims);
    }

    [Fact]
    public void BuildViewportDelta_Always_includes_the_players_own_sim()
    {
        var previous = Snapshot(1, Sim(1, 0, 0), Sim(2, 100, 100));
        var current = Snapshot(2, Sim(1, 0, 0), Sim(2, 101, 101));

        var delta = AoiFilter.BuildViewportDelta(previous, current, new Viewport(0, 0, 5), alwaysIncludeSim: 2);

        Assert.Contains(delta.Sims, sim => sim.NetworkId == 2);
        Assert.DoesNotContain(2UL, delta.RemovedSims);
    }

    [Fact]
    public void BuildViewportDelta_Uses_exit_margin_for_hysteresis()
    {
        var viewport = new Viewport(0, 0, 5) { ExitMargin = 2 };

        var previous = Snapshot(1, Sim(1, 5, 0));
        var insideMargin = Snapshot(2, Sim(1, 6, 0));
        var outsideMargin = Snapshot(3, Sim(1, 8, 0));

        var staying = AoiFilter.BuildViewportDelta(previous, insideMargin, viewport);
        var leaving = AoiFilter.BuildViewportDelta(insideMargin, outsideMargin, viewport);

        Assert.Contains(staying.Sims, sim => sim.NetworkId == 1);
        Assert.Empty(staying.RemovedSims);
        Assert.Contains(1UL, leaving.RemovedSims);
    }

    [Fact]
    public void BuildViewportDelta_Falls_back_to_a_filtered_full_snapshot_when_history_is_missing()
    {
        var current = Snapshot(2, Sim(1, 0, 0), Sim(2, 100, 100));

        var delta = AoiFilter.BuildViewportDelta(null, current, new Viewport(0, 0, 5));

        Assert.True(delta.Full);
        Assert.Single(delta.Sims);
        Assert.Equal(1UL, delta.Sims[0].NetworkId);
    }
}

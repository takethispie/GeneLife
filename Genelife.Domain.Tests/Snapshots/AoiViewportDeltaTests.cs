using Genelife.Api.DTOs;
using Genelife.Api.Snapshots;

namespace Genelife.UnitTests.Snapshots;

public class AoiViewportDeltaTests
{
    private static WorldSnapshot Snapshot(long tick, params GenosStatus[] genoses) =>
        new(tick, 2, genoses.ToList(), new List<CompanyStatus>());

    private static GenosStatus Genos(ulong id, int x, int y) => new()
    {
        NetworkId = id,
        Name = $"Genos_{id}",
        TileX = x,
        TileY = y,
        X = x,
        Y = y
    };

    [Fact]
    public void BuildViewportDelta_Includes_genos_that_entered_the_viewport()
    {
        var previous = Snapshot(1, Genos(1, 100, 100));
        var current = Snapshot(2, Genos(1, 2, 2));

        var delta = AoiFilter.BuildViewportDelta(previous, current, new Viewport(0, 0, 5));

        Assert.False(delta.Full);
        Assert.Contains(delta.Genoses, genos => genos.NetworkId == 1);
        Assert.Empty(delta.RemovedGenoses);
    }

    [Fact]
    public void BuildViewportDelta_Removes_genos_that_left_the_viewport()
    {
        var previous = Snapshot(1, Genos(1, 2, 2));
        var current = Snapshot(2, Genos(1, 100, 100));

        var delta = AoiFilter.BuildViewportDelta(previous, current, new Viewport(0, 0, 5));

        Assert.Empty(delta.Genoses);
        Assert.Contains(1UL, delta.RemovedGenoses);
    }

    [Fact]
    public void BuildViewportDelta_Always_includes_the_players_own_genos()
    {
        var previous = Snapshot(1, Genos(1, 0, 0), Genos(2, 100, 100));
        var current = Snapshot(2, Genos(1, 0, 0), Genos(2, 101, 101));

        var delta = AoiFilter.BuildViewportDelta(previous, current, new Viewport(0, 0, 5), alwaysIncludeGenos: 2);

        Assert.Contains(delta.Genoses, genos => genos.NetworkId == 2);
        Assert.DoesNotContain(2UL, delta.RemovedGenoses);
    }

    [Fact]
    public void BuildViewportDelta_Uses_exit_margin_for_hysteresis()
    {
        var viewport = new Viewport(0, 0, 5) { ExitMargin = 2 };

        var previous = Snapshot(1, Genos(1, 5, 0));
        var insideMargin = Snapshot(2, Genos(1, 6, 0));
        var outsideMargin = Snapshot(3, Genos(1, 8, 0));

        var staying = AoiFilter.BuildViewportDelta(previous, insideMargin, viewport);
        var leaving = AoiFilter.BuildViewportDelta(insideMargin, outsideMargin, viewport);

        Assert.Contains(staying.Genoses, genos => genos.NetworkId == 1);
        Assert.Empty(staying.RemovedGenoses);
        Assert.Contains(1UL, leaving.RemovedGenoses);
    }

    [Fact]
    public void BuildViewportDelta_Falls_back_to_a_filtered_full_snapshot_when_history_is_missing()
    {
        var current = Snapshot(2, Genos(1, 0, 0), Genos(2, 100, 100));

        var delta = AoiFilter.BuildViewportDelta(null, current, new Viewport(0, 0, 5));

        Assert.True(delta.Full);
        Assert.Single(delta.Genoses);
        Assert.Equal(1UL, delta.Genoses[0].NetworkId);
    }
}

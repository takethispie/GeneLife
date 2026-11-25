using Genelife.Api.DTOs;
using Genelife.Api.Snapshots;

namespace Genelife.UnitTests.Snapshots;

public class SnapshotHistoryTests
{
    private static WorldSnapshot Snapshot(long tick, params GenosStatus[] genoses) =>
        new(tick, WorldSnapshot.CurrentVersion, genoses.ToList(), new List<CompanyStatus>());

    private static GenosStatus Genos(ulong id, float x = 0, float y = 0, float hunger = 50) => new()
    {
        NetworkId = id,
        Name = $"Genos_{id}",
        Hunger = hunger,
        Energy = 50,
        Hygiene = 50,
        Bladder = 50,
        CurrentAction = "Idle",
        TimeRemaining = 0,
        X = x,
        Y = y
    };

    [Fact]
    public void Record_StoresSnapshotsByTick()
    {
        var history = new SnapshotHistory(capacity: 8);
        history.Record(Snapshot(1, Genos(1)));
        history.Record(Snapshot(2, Genos(1, hunger: 40)));

        Assert.True(history.TryGet(1, out var first));
        Assert.Equal(50, first.Genoses[0].Hunger);
        Assert.True(history.TryGet(2, out var second));
        Assert.Equal(40, second.Genoses[0].Hunger);
        Assert.Equal(2, history.LatestTick);
    }

    [Fact]
    public void BuildDelta_FromRetainedTick_ReturnsOnlyChanges()
    {
        var history = new SnapshotHistory(capacity: 8);
        history.Record(Snapshot(10, Genos(1), Genos(2)));
        history.Record(Snapshot(11, Genos(1, hunger: 30), Genos(3)));

        var delta = history.BuildDelta(10);

        Assert.NotNull(delta);
        Assert.False(delta!.Full);
        Assert.Equal(11, delta.Tick);
        Assert.Equal(10, delta.SinceTick);
        Assert.Contains(delta.Genoses, genos => genos.NetworkId == 1 && genos.Hunger == 30);
        Assert.Contains(delta.Genoses, genos => genos.NetworkId == 3);
        Assert.DoesNotContain(delta.Genoses, genos => genos.NetworkId == 2);
        Assert.Contains((ulong)2, delta.RemovedGenoses);
    }

    [Fact]
    public void BuildDelta_IncludesPositionsAndTick()
    {
        var history = new SnapshotHistory(capacity: 8);
        history.Record(Snapshot(1, Genos(1, x: 0, y: 0)));
        history.Record(Snapshot(2, Genos(1, x: 12.5f, y: -3.25f)));

        var delta = history.BuildDelta(1);

        Assert.NotNull(delta);
        var genos = Assert.Single(delta!.Genoses);
        Assert.Equal(12.5f, genos.X);
        Assert.Equal(-3.25f, genos.Y);
        Assert.Equal(2, delta.Tick);
    }

    [Fact]
    public void BuildDelta_ForUnknownTick_ReturnsFullSnapshot()
    {
        var history = new SnapshotHistory(capacity: 8);
        history.Record(Snapshot(5, Genos(1), Genos(2)));

        var delta = history.BuildDelta(0);

        Assert.NotNull(delta);
        Assert.True(delta!.Full);
        Assert.Equal(5, delta.Tick);
        Assert.Equal(0, delta.SinceTick);
        Assert.Equal(2, delta.Genoses.Count);
    }

    [Fact]
    public void BuildDelta_OutsideRetentionWindow_ReturnsFullSnapshot()
    {
        var history = new SnapshotHistory(capacity: 4);
        for (var tick = 1; tick <= 20; tick++)
            history.Record(Snapshot(tick, Genos(1, hunger: tick)));

        var delta = history.BuildDelta(1);

        Assert.NotNull(delta);
        Assert.True(delta!.Full);
        Assert.Equal(20, delta.Tick);
    }

    [Fact]
    public void BuildDelta_WhenClientIsCurrent_ReturnsEmptyDelta()
    {
        var history = new SnapshotHistory(capacity: 8);
        history.Record(Snapshot(7, Genos(1)));

        var delta = history.BuildDelta(7);

        Assert.NotNull(delta);
        Assert.True(delta!.Full is false);
        Assert.Empty(delta.Genoses);
        Assert.Empty(delta.RemovedGenoses);
    }

    [Fact]
    public void Clear_RemovesRetainedHistory()
    {
        var history = new SnapshotHistory(capacity: 8);
        history.Record(Snapshot(1, Genos(1)));

        history.Clear();

        Assert.Equal(-1, history.LatestTick);
        Assert.False(history.TryGet(1, out _));
        Assert.Null(history.BuildDelta(0));
    }
}

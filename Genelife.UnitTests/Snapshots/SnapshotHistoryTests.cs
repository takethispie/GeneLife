using Genelife.Api.DTOs;
using Genelife.Api.Snapshots;

namespace Genelife.UnitTests.Snapshots;

public class SnapshotHistoryTests
{
    private static WorldSnapshot Snapshot(long tick, params SimStatus[] sims) =>
        new(tick, WorldSnapshot.CurrentVersion, sims.ToList(), new List<CompanyStatus>());

    private static SimStatus Sim(ulong id, float x = 0, float y = 0, float hunger = 50) => new()
    {
        NetworkId = id,
        Name = $"Sim_{id}",
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
        history.Record(Snapshot(1, Sim(1)));
        history.Record(Snapshot(2, Sim(1, hunger: 40)));

        Assert.True(history.TryGet(1, out var first));
        Assert.Equal(50, first.Sims[0].Hunger);
        Assert.True(history.TryGet(2, out var second));
        Assert.Equal(40, second.Sims[0].Hunger);
        Assert.Equal(2, history.LatestTick);
    }

    [Fact]
    public void BuildDelta_FromRetainedTick_ReturnsOnlyChanges()
    {
        var history = new SnapshotHistory(capacity: 8);
        history.Record(Snapshot(10, Sim(1), Sim(2)));
        history.Record(Snapshot(11, Sim(1, hunger: 30), Sim(3)));

        var delta = history.BuildDelta(10);

        Assert.NotNull(delta);
        Assert.False(delta!.Full);
        Assert.Equal(11, delta.Tick);
        Assert.Equal(10, delta.SinceTick);
        Assert.Contains(delta.Sims, sim => sim.NetworkId == 1 && sim.Hunger == 30);
        Assert.Contains(delta.Sims, sim => sim.NetworkId == 3);
        Assert.DoesNotContain(delta.Sims, sim => sim.NetworkId == 2);
        Assert.Contains((ulong)2, delta.RemovedSims);
    }

    [Fact]
    public void BuildDelta_IncludesPositionsAndTick()
    {
        var history = new SnapshotHistory(capacity: 8);
        history.Record(Snapshot(1, Sim(1, x: 0, y: 0)));
        history.Record(Snapshot(2, Sim(1, x: 12.5f, y: -3.25f)));

        var delta = history.BuildDelta(1);

        Assert.NotNull(delta);
        var sim = Assert.Single(delta!.Sims);
        Assert.Equal(12.5f, sim.X);
        Assert.Equal(-3.25f, sim.Y);
        Assert.Equal(2, delta.Tick);
    }

    [Fact]
    public void BuildDelta_ForUnknownTick_ReturnsFullSnapshot()
    {
        var history = new SnapshotHistory(capacity: 8);
        history.Record(Snapshot(5, Sim(1), Sim(2)));

        var delta = history.BuildDelta(0);

        Assert.NotNull(delta);
        Assert.True(delta!.Full);
        Assert.Equal(5, delta.Tick);
        Assert.Equal(0, delta.SinceTick);
        Assert.Equal(2, delta.Sims.Count);
    }

    [Fact]
    public void BuildDelta_OutsideRetentionWindow_ReturnsFullSnapshot()
    {
        var history = new SnapshotHistory(capacity: 4);
        for (var tick = 1; tick <= 20; tick++)
            history.Record(Snapshot(tick, Sim(1, hunger: tick)));

        var delta = history.BuildDelta(1);

        Assert.NotNull(delta);
        Assert.True(delta!.Full);
        Assert.Equal(20, delta.Tick);
    }

    [Fact]
    public void BuildDelta_WhenClientIsCurrent_ReturnsEmptyDelta()
    {
        var history = new SnapshotHistory(capacity: 8);
        history.Record(Snapshot(7, Sim(1)));

        var delta = history.BuildDelta(7);

        Assert.NotNull(delta);
        Assert.True(delta!.Full is false);
        Assert.Empty(delta.Sims);
        Assert.Empty(delta.RemovedSims);
    }

    [Fact]
    public void Clear_RemovesRetainedHistory()
    {
        var history = new SnapshotHistory(capacity: 8);
        history.Record(Snapshot(1, Sim(1)));

        history.Clear();

        Assert.Equal(-1, history.LatestTick);
        Assert.False(history.TryGet(1, out _));
        Assert.Null(history.BuildDelta(0));
    }
}

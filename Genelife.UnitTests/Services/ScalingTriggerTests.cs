using Genelife.Api.Services;
using Genelife.Api.Snapshots;

namespace Genelife.UnitTests.Services;

public class ScalingTriggerTests
{
    private static MetricsSnapshot Metrics(
        int entityCount = 1_000,
        double p95 = 5,
        params SystemMetric[] systems) => new(
            TickCount: 1,
            WindowTickCount: 1,
            WindowSeconds: 1,
            TickRatePerSecond: 20,
            LastTickMilliseconds: p95,
            AverageTickMilliseconds: p95,
            P50TickMilliseconds: p95,
            P95TickMilliseconds: p95,
            P99TickMilliseconds: p95,
            TotalAllocatedBytes: 0,
            WindowAllocatedBytes: 0,
            LastTickAllocatedBytes: 0,
            AllocatedBytesPerTick: 0,
            Gen0Collections: 0,
            Gen1Collections: 0,
            Gen2Collections: 0,
            Gen0CollectionsPerMinute: 0,
            Gen1CollectionsPerMinute: 0,
            Gen2CollectionsPerMinute: 0,
            EntityCount: entityCount,
            EntitiesCreated: 0,
            EntitiesDestroyed: 0,
            EntitiesCreatedPerMinute: 0,
            EntitiesDestroyedPerMinute: 0,
            Systems: systems);

    private static ReplicationMetricsSnapshot Replication(double bytesPerClientPerSecond = 100) => new(
        FullSnapshots: 1,
        DeltaSnapshots: 1,
        FullBytes: 1000,
        DeltaBytes: 100,
        TotalBytes: 1100,
        ActiveClients: 32,
        BytesPerSecond: bytesPerClientPerSecond,
        BytesPerClientPerSecond: bytesPerClientPerSecond);

    private static SystemMetric System(string name, double totalMilliseconds, long allocated = 0) => new(
        Name: name,
        Calls: 1,
        TotalMilliseconds: totalMilliseconds,
        AverageMilliseconds: totalMilliseconds,
        MaxMilliseconds: totalMilliseconds,
        AllocatedBytes: allocated,
        AverageAllocatedBytes: allocated);

    [Fact]
    public void Evaluate_NoTrigger_ReturnsNone()
    {
        var decision = ScalingTriggers.Evaluate(Metrics(), Replication());

        Assert.False(decision.ShouldApply);
        Assert.Equal(ScalingAction.None, decision.Action);
    }

    [Fact]
    public void Evaluate_ExtremeTickP95_RecommendsLoadShedding()
    {
        var decision = ScalingTriggers.Evaluate(Metrics(p95: 200), Replication());

        Assert.True(decision.ShouldApply);
        Assert.Equal(ScalingAction.LoadShedding, decision.Action);
    }

    [Fact]
    public void Evaluate_ExcessBandwidth_RecommendsInterestManagement()
    {
        var decision = ScalingTriggers.Evaluate(Metrics(), Replication(bytesPerClientPerSecond: 100_000));

        Assert.True(decision.ShouldApply);
        Assert.Equal(ScalingAction.InterestManagement, decision.Action);
    }

    [Fact]
    public void Evaluate_DecisionSystemDominatesAtScale_RecommendsSpatialGrid()
    {
        var metrics = Metrics(
            entityCount: 20_000,
            p95: 100,
            System("Decision", 80),
            System("NeedsDecay", 20));

        var decision = ScalingTriggers.Evaluate(metrics, Replication());

        Assert.True(decision.ShouldApply);
        Assert.Equal(ScalingAction.SpatialGrid, decision.Action);
        Assert.Contains("Decision", decision.Trigger);
    }

    [Fact]
    public void Evaluate_NonNeighborDominantSystem_RecommendsParallelPureSystems()
    {
        var metrics = Metrics(
            entityCount: 20_000,
            p95: 100,
            System("Action", 70),
            System("NeedsDecay", 30));

        var decision = ScalingTriggers.Evaluate(metrics, Replication());

        Assert.True(decision.ShouldApply);
        Assert.Equal(ScalingAction.ParallelPureSystems, decision.Action);
    }
}

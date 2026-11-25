using Genelife.Api.Snapshots;

namespace Genelife.Api.Services;

public sealed record ScalingThresholds(
    int NpcCount,
    double TickP95Milliseconds,
    double SnapshotBytesPerClientPerSecond,
    double DominantSystemShare)
{
    public static ScalingThresholds Default { get; } = new(
        NpcCount: 10_000,
        TickP95Milliseconds: 50,
        SnapshotBytesPerClientPerSecond: 64 * 1024,
        DominantSystemShare: 0.35);
}

public enum ScalingAction
{
    None = 0,
    SpatialGrid,
    InterestManagement,
    ParallelPureSystems,
    LoadShedding
}

public sealed record ScalingDecision(ScalingAction Action, string Trigger, bool ShouldApply);

/// <summary>
/// Wave 5 / Perf12 is trigger-gated: expensive scaling work (spatial queries,
/// AOI, parallelism, load shedding) is only recommended when measured metrics
/// cross explicit thresholds. This evaluator is intentionally side-effect free
/// so decisions can be recorded and reviewed before enabling work.
/// </summary>
public static class ScalingTriggers
{
    public static ScalingDecision Evaluate(
        MetricsSnapshot metrics,
        ReplicationMetricsSnapshot replication,
        ScalingThresholds? thresholds = null)
    {
        thresholds ??= ScalingThresholds.Default;

        if (metrics.P95TickMilliseconds > thresholds.TickP95Milliseconds * 3)
        {
            return new ScalingDecision(
                ScalingAction.LoadShedding,
                $"tick p95 {metrics.P95TickMilliseconds:F1}ms > {thresholds.TickP95Milliseconds * 3:F1}ms (3x target)",
                true);
        }

        if (replication.BytesPerClientPerSecond > thresholds.SnapshotBytesPerClientPerSecond)
        {
            return new ScalingDecision(
                ScalingAction.InterestManagement,
                $"snapshot bytes/client {replication.BytesPerClientPerSecond:F0}/s > {thresholds.SnapshotBytesPerClientPerSecond:F0}/s",
                true);
        }

        if (metrics.EntityCount >= thresholds.NpcCount && metrics.P95TickMilliseconds > thresholds.TickP95Milliseconds)
        {
            var dominant = DominantSystem(metrics);
            if (dominant is { } top && top.Share >= thresholds.DominantSystemShare)
            {
                var action = top.Name is "Decision"
                    ? ScalingAction.SpatialGrid
                    : ScalingAction.ParallelPureSystems;

                return new ScalingDecision(
                    action,
                    $"{top.Name} is {top.Share:P0} of system time at {metrics.EntityCount} NPCs (p95 {metrics.P95TickMilliseconds:F1}ms)",
                    true);
            }

            return new ScalingDecision(
                ScalingAction.ParallelPureSystems,
                $"{metrics.EntityCount} NPCs at p95 {metrics.P95TickMilliseconds:F1}ms > {thresholds.TickP95Milliseconds:F1}ms",
                true);
        }

        return new ScalingDecision(ScalingAction.None, "no trigger met", false);
    }

    private static (string Name, double Share)? DominantSystem(MetricsSnapshot metrics)
    {
        var total = metrics.Systems.Sum(system => system.TotalMilliseconds);
        if (total <= 0 || metrics.Systems.Count == 0)
            return null;

        var top = metrics.Systems.MaxBy(system => system.TotalMilliseconds)!;
        return (top.Name, top.TotalMilliseconds / total);
    }
}

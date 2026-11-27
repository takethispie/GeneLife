namespace Genelife.Api.Services;

/// <summary>
/// Per-NPC-tier performance budgets. The load harness is compared against these
/// so a regression in tick p95 or per-tick allocation fails the guardrail run
/// instead of landing unnoticed.
///
/// Tick p95 is the accepted gate. p99 is reported by the harness for diagnosis only
/// and is deliberately not budgeted: the measured tail sits an order of magnitude
/// above p95 and varies far more between runs than p95 does, so a p99 threshold
/// would fail spuriously rather than report a regression. Treat a tail as a
/// specific spike to investigate, not as a threshold to track here.
/// </summary>
public sealed record PerformanceBudget(
    int NpcCount,
    double MaxP95Milliseconds,
    double MaxAllocatedBytesPerTick);

public static class PerformanceThresholds
{
    public static IReadOnlyList<PerformanceBudget> Default { get; } = new[]
    {
        new PerformanceBudget(NpcCount: 1_000, MaxP95Milliseconds: 10, MaxAllocatedBytesPerTick: 300_000),
        new PerformanceBudget(NpcCount: 2_000, MaxP95Milliseconds: 15, MaxAllocatedBytesPerTick: 600_000),
        new PerformanceBudget(NpcCount: 5_000, MaxP95Milliseconds: 25, MaxAllocatedBytesPerTick: 1_500_000),
        new PerformanceBudget(NpcCount: 10_000, MaxP95Milliseconds: 50, MaxAllocatedBytesPerTick: 3_000_000)
    };

    public static PerformanceBudget? For(int npcCount) =>
        Default.FirstOrDefault(budget => budget.NpcCount == npcCount);

    /// <summary>
    /// Returns null when the measured tick fits the tier budget, otherwise a
    /// human-readable failure reason.
    /// </summary>
    public static string? Evaluate(int npcCount, double p95Milliseconds, double allocatedBytesPerTick)
    {
        var budget = For(npcCount);
        if (budget is null)
            return null;

        if (p95Milliseconds > budget.MaxP95Milliseconds)
        {
            return $"tick p95 {p95Milliseconds:F2}ms exceeds {budget.MaxP95Milliseconds:F2}ms budget at {npcCount} NPCs";
        }

        if (allocatedBytesPerTick > budget.MaxAllocatedBytesPerTick)
        {
            return $"allocation {allocatedBytesPerTick:F0}B/tick exceeds {budget.MaxAllocatedBytesPerTick:F0}B budget at {npcCount} NPCs";
        }

        return null;
    }
}

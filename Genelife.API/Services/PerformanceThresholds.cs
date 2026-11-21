namespace Genelife.Api.Services;

/// <summary>
/// Per-NPC-tier performance budgets. The load harness is compared against these
/// so a regression in tick p95 or per-tick allocation fails the guardrail run
/// instead of landing unnoticed.
/// </summary>
public sealed record PerformanceBudget(
    int NpcCount,
    double MaxP95Milliseconds,
    double MaxAllocatedBytesPerTick);

public static class PerformanceThresholds
{
    public static IReadOnlyList<PerformanceBudget> Default { get; } = new[]
    {
        new PerformanceBudget(NpcCount: 1_000, MaxP95Milliseconds: 10, MaxAllocatedBytesPerTick: 150_000),
        new PerformanceBudget(NpcCount: 5_000, MaxP95Milliseconds: 25, MaxAllocatedBytesPerTick: 800_000),
        new PerformanceBudget(NpcCount: 10_000, MaxP95Milliseconds: 50, MaxAllocatedBytesPerTick: 1_600_000)
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

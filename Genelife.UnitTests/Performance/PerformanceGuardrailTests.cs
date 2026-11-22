using System.Text.Json;
using Genelife.Api.Services;

namespace Genelife.UnitTests.Performance;

/// <summary>
/// CI guardrail: runs the load harness and fails on a per-tier budget breach or
/// a regression beyond the stored baseline tolerance.
/// </summary>
public class PerformanceGuardrailTests
{
    private const int TierEntityCount = 1_000;

    [Fact]
    public void Fixture_1k_MeetsTierBudgetAndStoredBaseline()
    {
        var report = LoadHarness.RunHarness(TierEntityCount, synthetic: true);

        var budgetFailure = PerformanceThresholds.Evaluate(
            TierEntityCount,
            report.Results.P95TickMs,
            report.Results.AllocatedBytesPerTick);
        Assert.True(budgetFailure is null, budgetFailure);

        var baseline = LoadBaseline();
        if (baseline is null)
            return;

        var p95Limit = baseline.P95Ms * baseline.ToleranceMultiplier;
        var allocationLimit = baseline.AllocatedBytesPerTick * baseline.ToleranceMultiplier;

        Assert.True(report.Results.P95TickMs <= p95Limit,
            $"tick p95 {report.Results.P95TickMs:F2}ms regressed beyond baseline {baseline.P95Ms:F2}ms x{baseline.ToleranceMultiplier:F1}");

        Assert.True(report.Results.AllocatedBytesPerTick <= allocationLimit,
            $"allocation {report.Results.AllocatedBytesPerTick:F0}B/tick regressed beyond baseline {baseline.AllocatedBytesPerTick:F0}B x{baseline.ToleranceMultiplier:F1}");
    }

    [Fact]
    public void Fixture_2k_MeetsTierBudget()
    {
        var report = LoadHarness.RunHarness(2_000, synthetic: true);

        var budgetFailure = PerformanceThresholds.Evaluate(
            2_000,
            report.Results.P95TickMs,
            report.Results.AllocatedBytesPerTick);
        Assert.True(budgetFailure is null, budgetFailure);
    }

    [Fact]
    public void Fixture_5k_MeetsTierBudget()
    {
        var report = LoadHarness.RunHarness(5_000, synthetic: true);

        var budgetFailure = PerformanceThresholds.Evaluate(
            5_000,
            report.Results.P95TickMs,
            report.Results.AllocatedBytesPerTick);
        Assert.True(budgetFailure is null, budgetFailure);
    }

    private static Baseline? LoadBaseline()
    {
        var path = FindBaselinePath();
        if (path is null)
            return null;

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Baseline>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private static string? FindBaselinePath()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "performance-baselines",
                "1k-synthetic-baseline.json");

            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        return null;
    }

    private sealed class Baseline
    {
        public int EntityCount { get; set; }
        public string Workload { get; set; } = string.Empty;
        public double P95Ms { get; set; }
        public double AllocatedBytesPerTick { get; set; }
        public double ToleranceMultiplier { get; set; } = 2.0;
    }
}

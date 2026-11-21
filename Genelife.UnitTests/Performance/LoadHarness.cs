using System.Diagnostics;
using System.Text.Json;
using Arch.Core;
using Genelife.Api.Services;
using Genelife.Components.Employment;
using Genelife.Systems;

namespace Genelife.UnitTests.Performance;

public class LoadHarness
{
    private const int TicksToRun = 10;
    private const float DeltaTime = 1f / 20f;

    [Fact]
    public void Fixture_1k_Synthetic_Report()
    {
        var report = RunHarness(1_000, synthetic: true);
        WriteReport("load_1k_synthetic", report);
        Assert.True(report.Results.TickRatePerSecond > 0);
    }

    [Fact]
    public void Fixture_5k_Synthetic_Report()
    {
        var report = RunHarness(5_000, synthetic: true);
        WriteReport("load_5k_synthetic", report);
        Assert.True(report.Results.TickRatePerSecond > 0);
    }

    [Fact(Skip = "Manual run only: 10k entities")]
    public void Fixture_10k_Synthetic_Report()
    {
        var report = RunHarness(10_000, synthetic: true);
        WriteReport("load_10k_synthetic", report);
        Assert.True(report.Results.TickRatePerSecond > 0);
    }

    [Fact]
    public void Fixture_1k_Realistic_Report()
    {
        var report = RunHarness(1_000, synthetic: false);
        WriteReport("load_1k_realistic", report);
        Assert.True(report.Results.TickRatePerSecond > 0);
    }

    [Fact]
    public void Fixture_5k_Realistic_Report()
    {
        var report = RunHarness(5_000, synthetic: false);
        WriteReport("load_5k_realistic", report);
        Assert.True(report.Results.TickRatePerSecond > 0);
    }

    [Fact(Skip = "Manual run only: 10k entities")]
    public void Fixture_10k_Realistic_Report()
    {
        var report = RunHarness(10_000, synthetic: false);
        WriteReport("load_10k_realistic", report);
        Assert.True(report.Results.TickRatePerSecond > 0);
    }

    public static HarnessReport RunHarness(int count, bool synthetic)
    {
        var world = World.Create();
        var metrics = new Metrics(histogramCapacity: 256, logEveryTicks: int.MaxValue);
        var employmentIndex = new EmploymentIndex();

        var needsDecay = new NeedsDecaySystem(world);
        var decision = new DecisionSystem(world);
        var action = new ActionSystem(world);
        var hiring = new HiringSystem(world, employmentIndex);
        var payroll = new PayrollSystem(world);
        var jobSeeker = new JobSeekerSystem(world);

        if (synthetic)
            SimFixture.PopulateSynthetic(world, count);
        else
            SimFixture.PopulateRealistic(world, count);

        var startEntityCount = world.Size;

        for (int i = 0; i < TicksToRun; i++)
        {
            var tickStart = Stopwatch.GetTimestamp();
            var allocStart = GC.GetAllocatedBytesForCurrentThread();

            needsDecay.Update(DeltaTime);
            decision.Update();
            action.Update(DeltaTime);
            hiring.Update();
            payroll.Update();
            jobSeeker.Update();

            var allocBytes = GC.GetAllocatedBytesForCurrentThread() - allocStart;
            if (allocBytes < 0) allocBytes = 0;
            var durationMs = Stopwatch.GetElapsedTime(tickStart).TotalMilliseconds;
            metrics.RecordTick(durationMs, allocBytes, world.Size);
        }

        var snapshot = metrics.Snapshot();

        world.Dispose();

        return new HarnessReport
        {
            Config = new HarnessConfig
            {
                EntityCount = count,
                WorkloadType = synthetic ? "synthetic" : "realistic",
                TicksRun = TicksToRun,
                DeltaTime = DeltaTime
            },
            Results = new HarnessResults
            {
                AverageTickMs = snapshot.AverageTickMilliseconds,
                P50TickMs = snapshot.P50TickMilliseconds,
                P95TickMs = snapshot.P95TickMilliseconds,
                P99TickMs = snapshot.P99TickMilliseconds,
                TickRatePerSecond = snapshot.TickRatePerSecond,
                AllocatedBytesPerTick = snapshot.AllocatedBytesPerTick,
                Gen0Collections = snapshot.Gen0Collections,
                Gen1Collections = snapshot.Gen1Collections,
                Gen2Collections = snapshot.Gen2Collections,
                Gen0PerMinute = snapshot.Gen0CollectionsPerMinute,
                Gen1PerMinute = snapshot.Gen1CollectionsPerMinute,
                Gen2PerMinute = snapshot.Gen2CollectionsPerMinute,
                EntityCount = snapshot.EntityCount,
                StartEntityCount = startEntityCount
            }
        };
    }

    private static void WriteReport(string name, HarnessReport report)
    {
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var dir = Path.Combine(AppContext.BaseDirectory, "performance-reports");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{name}.json");
        File.WriteAllText(path, json);
        Console.WriteLine($"[harness] {name}: p50={report.Results.P50TickMs:F2}ms p95={report.Results.P95TickMs:F2}ms rate={report.Results.TickRatePerSecond:F1}/s alloc/tick={report.Results.AllocatedBytesPerTick:F0}B entities={report.Results.EntityCount}");
    }

    public sealed class HarnessReport
    {
        public HarnessConfig Config { get; set; } = null!;
        public HarnessResults Results { get; set; } = null!;
    }

    public sealed class HarnessConfig
    {
        public int EntityCount { get; set; }
        public string WorkloadType { get; set; } = null!;
        public int TicksRun { get; set; }
        public float DeltaTime { get; set; }
    }

    public sealed class HarnessResults
    {
        public double AverageTickMs { get; set; }
        public double P50TickMs { get; set; }
        public double P95TickMs { get; set; }
        public double P99TickMs { get; set; }
        public double TickRatePerSecond { get; set; }
        public double AllocatedBytesPerTick { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
        public double Gen0PerMinute { get; set; }
        public double Gen1PerMinute { get; set; }
        public double Gen2PerMinute { get; set; }
        public int EntityCount { get; set; }
        public int StartEntityCount { get; set; }
    }
}
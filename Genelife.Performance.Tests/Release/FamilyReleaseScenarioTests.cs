using System.Diagnostics;
using System.Text.Json;
using Genelife.Api.Services;
using Genelife.Family;

namespace Genelife.UnitTests.Release;

[Trait("Category", "Release")]
public class FamilyReleaseScenarioTests
{
    private const float DeltaTime = 1f / 20f;
    private const int Seed = 21;

    // Generous regression guard only; correctness assertions below are exact.
    private const double P95TickBudgetMilliseconds = 100;

    [Fact]
    public void TwoThousandSimsForTenYears()
    {
        var elapsed = Stopwatch.StartNew();
        using var engine = Build(sims: 2_000, seed: Seed);

        var progress = new List<object>();
        var status = engine.BuildFamilyStatus();
        for (var year = 1; year <= 10; year++)
        {
            Run(engine, years: 1);
            status = engine.BuildFamilyStatus();
            var yearHistory = engine.Economy!.Life.MetricHistory;
            var yearLatest = yearHistory.Count > 0 ? yearHistory[^1] : null;
            progress.Add(new
            {
                year,
                population = status.Population,
                p95TickMilliseconds = status.Performance.P95TickMilliseconds,
                p99TickMilliseconds = status.Performance.P99TickMilliseconds,
                candidateAverage = status.Performance.CandidateAverage,
                candidateP95 = status.Performance.CandidateP95,
                candidateMax = status.Performance.CandidateMax,
                candidateCap = status.Performance.CandidateCap,
                candidateSims = status.Performance.CandidateSims,
                birthsThisYear = yearLatest?.BirthsThisYear ?? 0,
                deathsThisYear = yearLatest?.DeathsThisYear ?? 0,
                orphans = status.Orphans,
                stateCareChildren = status.StateCareChildren,
                moneySupplyCents = engine.Economy.TotalMoneySupply().Cents
            });
            WriteReport("two-thousand-sims-10-years-progress", new
            {
                scenario = nameof(TwoThousandSimsForTenYears),
                seed = Seed,
                sims = 2_000,
                completedYears = year,
                years = progress
            });
        }

        var metrics = engine.GetMetricsSnapshot();
        var supply = engine.Economy!.TotalMoneySupply();
        var history = engine.Economy.Life.MetricHistory;

        var capturedPopulation = status.Population;
        var state = engine.CaptureState(engine.NetworkIds);
        engine.Shutdown();

        using var restored = new SimulationEngine();
        var restoreError = Record.Exception(() => restored.Restore(state, restored.NetworkIds));
        var restoredPopulation = restoreError is null ? restored.BuildFamilyStatus().Population : -1;

        elapsed.Stop();
        var latest = history.Count > 0 ? history[^1] : null;
        WriteReport("two-thousand-sims-10-years", new
        {
            scenario = nameof(TwoThousandSimsForTenYears),
            seed = Seed,
            sims = 2_000,
            years = 10,
            elapsedMilliseconds = elapsed.Elapsed.TotalMilliseconds,
            population = status.Population,
            p95TickMilliseconds = status.Performance.P95TickMilliseconds,
            p99TickMilliseconds = status.Performance.P99TickMilliseconds,
            candidateAverage = status.Performance.CandidateAverage,
            candidateP95 = status.Performance.CandidateP95,
            candidateMax = status.Performance.CandidateMax,
            candidateCap = status.Performance.CandidateCap,
            candidateSims = status.Performance.CandidateSims,
            birthsThisYear = latest?.BirthsThisYear ?? 0,
            deathsThisYear = latest?.DeathsThisYear ?? 0,
            birthsRecorded = history.Sum(snapshot => snapshot.BirthsThisYear),
            deathsRecorded = history.Sum(snapshot => snapshot.DeathsThisYear),
            orphans = status.Orphans,
            stateCareChildren = status.StateCareChildren,
            moneySupplyCents = supply.Cents,
            moneySupplyUnits = supply.Units,
            metricYears = history.Count,
            healthFailures = status.Health.Count(check => check.Level == "Fail"),
            healthWarnings = status.Health.Count(check => check.Level == "Warn"),
            healthFailureNames = status.Health.Where(check => check.Level == "Fail")
                .Select(check => check.Name).Distinct().ToList(),
            capturedPopulation,
            restoredPopulation,
            restoreSucceeded = restoreError is null,
            yearlyProgress = progress
        });

        Assert.True(status.Population > 0, $"population collapsed to {status.Population}");
        Assert.DoesNotContain(status.Health, check => check.Level == "Fail");
        Assert.True(status.Performance.CandidateMax <= status.Performance.CandidateCap,
            $"candidate max {status.Performance.CandidateMax} exceeds cap {status.Performance.CandidateCap}");
        Assert.True(status.Performance.CandidateAverage <= status.Performance.CandidateCap,
            $"candidate average {status.Performance.CandidateAverage} exceeds cap {status.Performance.CandidateCap}");
        Assert.True(supply.IsPositive, $"money supply is not positive: {supply.Cents} cents");
        Assert.True(metrics.P95TickMilliseconds <= P95TickBudgetMilliseconds,
            $"tick p95 {metrics.P95TickMilliseconds:F2}ms exceeds generous budget {P95TickBudgetMilliseconds:F0}ms at 2,000 sims");
        Assert.Null(restoreError);
        Assert.InRange(restoredPopulation, capturedPopulation / 2, capturedPopulation * 2);
    }

    [Fact]
    public void ThirtyYearContinuity()
    {
        var elapsed = Stopwatch.StartNew();
        using var engine = Build(sims: 200, seed: Seed);
        Run(engine, years: 30);

        var status = engine.BuildFamilyStatus();
        var supply = engine.Economy!.TotalMoneySupply();
        var history = engine.Economy.Life.MetricHistory;
        var continuity = ContinuityInvariants.Verify(engine.World!, engine.Relationships, engine.Economy);
        var reproduction = ReproductionInvariants.Verify(engine.World!, engine.Relationships, engine.Economy);

        var births = history.Sum(snapshot => snapshot.BirthsThisYear);
        var deaths = history.Sum(snapshot => snapshot.DeathsThisYear);

        elapsed.Stop();
        var latest = history.Count > 0 ? history[^1] : null;
        WriteReport("thirty-year-continuity", new
        {
            scenario = nameof(ThirtyYearContinuity),
            seed = Seed,
            sims = 200,
            years = 30,
            elapsedMilliseconds = elapsed.Elapsed.TotalMilliseconds,
            population = status.Population,
            birthsThisYear = latest?.BirthsThisYear ?? 0,
            deathsThisYear = latest?.DeathsThisYear ?? 0,
            birthsRecorded = births,
            deathsRecorded = deaths,
            orphans = status.Orphans,
            stateCareChildren = status.StateCareChildren,
            moneySupplyCents = supply.Cents,
            moneySupplyUnits = supply.Units,
            metricYears = history.Count,
            continuityLevels = continuity.GroupBy(issue => issue.Level)
                .ToDictionary(group => group.Key, group => group.Count()),
            reproductionLevels = reproduction.GroupBy(issue => issue.Level)
                .ToDictionary(group => group.Key, group => group.Count()),
            continuityFailures = continuity.Where(issue => issue.Level == "Fail")
                .Select(issue => issue.Name).Distinct().ToList(),
            reproductionFailures = reproduction.Where(issue => issue.Level == "Fail")
                .Select(issue => issue.Name).Distinct().ToList()
        });

        Assert.True(status.Population > 0, $"population collapsed to {status.Population}");
        Assert.DoesNotContain(continuity, issue => issue.Level == "Fail");
        Assert.DoesNotContain(reproduction, issue => issue.Level == "Fail");
        Assert.True(supply.IsPositive, $"money supply is not positive: {supply.Cents} cents");
        Assert.NotEmpty(history);
        Assert.True(births > 0, "no births were recorded across the 30-year metric history");
        Assert.True(deaths > 0, "no deaths were recorded across the 30-year metric history");
    }

    private static SimulationEngine Build(int sims, int seed)
    {
        var engine = new SimulationEngine();
        engine.Initialize();
        engine.ExecuteWithWorld(world =>
        {
            var generator = new PopulationGenerator(
                world, engine.NetworkIds, engine.Economy!, engine.EmploymentIndex!,
                map: engine.MapContext,
                relationshipIndex: engine.Relationships,
                relationshipService: engine.RelationshipService!,
                relationshipOptions: engine.RelationshipOptions);
            generator.Generate(sims, seed: seed);
            return 0;
        });
        return engine;
    }

    private static void Run(SimulationEngine engine, int years)
    {
        var ticks = years * engine.Economy!.Clock.TicksPerYear;
        for (var i = 0; i < ticks; i++)
            engine.Tick(DeltaTime);
    }

    private static void WriteReport<T>(string scenario, T payload)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "release-reports");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{scenario}.json");
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
        Console.WriteLine($"[release-scenario] {scenario}: {json}");
    }
}

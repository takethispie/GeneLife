using System.Diagnostics;
using System.Text.Json;
using Genelife.Api.Services;
using Genelife.Family;
using Genelife.Performance;

namespace Genelife.UnitTests.Release;

/// <summary>
/// Records the before/after cohabitation comparison required before flipping
/// `CohabitOnPairing`. Both runs use the same 200-sim seed and 30-year horizon;
/// the test records the result and asserts invariants, not a preferred policy.
/// </summary>
[Trait("Category", "Release")]
public class CohabitationComparisonTests
{
    private const float DeltaTime = 1f / 20f;
    private const int Seed = 21;

    [Fact]
    public void ThirtyYearCohabitationComparison()
    {
        var off = Run(cohabit: false);
        var on = Run(cohabit: true);

        WriteReport(new
        {
            scenario = nameof(ThirtyYearCohabitationComparison),
            seed = Seed,
            sims = 200,
            years = 30,
            cohabitOff = off,
            cohabitOn = on
        });

        Assert.True(off.Population > 0);
        Assert.True(on.Population > 0);
        Assert.DoesNotContain("Fail", off.ContinuityFailures);
        Assert.DoesNotContain("Fail", on.ContinuityFailures);
    }

    private static CohortResult Run(bool cohabit)
    {
        using var engine = new SimulationEngine();
        engine.Initialize(
            relationshipOptions: new RelationshipOptions { CohabitOnPairing = cohabit });
        engine.ExecuteWithWorld(world =>
        {
            var generator = new PopulationGenerator(
                world, engine.NetworkIds, engine.Economy!, engine.EmploymentIndex!,
                map: engine.MapContext,
                relationshipIndex: engine.Relationships,
                relationshipService: engine.RelationshipService!,
                relationshipOptions: engine.RelationshipOptions);
            generator.Generate(200, seed: Seed);
            return 0;
        });

        var elapsed = Stopwatch.StartNew();
        var ticks = 30 * engine.Economy!.Clock.TicksPerYear;
        for (var i = 0; i < ticks; i++)
            engine.Tick(DeltaTime);
        elapsed.Stop();

        var status = engine.BuildFamilyStatus();
        var housing = engine.Economy.Housing.MetricHistory.Count > 0
            ? engine.Economy.Housing.MetricHistory[^1]
            : null;
        var life = engine.Economy.Life.MetricHistory;
        var economy = engine.Economy.MetricHistory.Count > 0
            ? engine.Economy.MetricHistory[^1]
            : null;
        var continuity = ContinuityInvariants.Verify(engine.World!, engine.Relationships, engine.Economy)
            .Where(issue => issue.Level == "Fail")
            .Select(issue => issue.Name)
            .Distinct()
            .ToArray();

        return new CohortResult(
            cohabit,
            status.Population,
            life.Sum(snapshot => snapshot.BirthsThisYear),
            life.Sum(snapshot => snapshot.DeathsThisYear),
            housing?.VacancyRate ?? 0m,
            housing?.OvercrowdedUnits ?? 0,
            housing?.HomelessHouseholds ?? 0,
            housing?.MedianRentBurden ?? 0m,
            economy?.Gini ?? 0m,
            engine.Economy.TotalMoneySupply().Cents,
            engine.GetMetricsSnapshot().P95TickMilliseconds,
            elapsed.Elapsed.TotalMilliseconds,
            continuity);
    }

    private static void WriteReport<T>(T payload)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "release-reports");
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(directory, "cohabitation-comparison.json"), json);
        Console.WriteLine($"[release-scenario] cohabitation-comparison: {json}");
    }

    private sealed record CohortResult(
        bool CohabitOnPairing,
        int Population,
        int Births,
        int Deaths,
        decimal HousingVacancyRate,
        int OvercrowdedUnits,
        int HomelessHouseholds,
        decimal MedianRentBurden,
        decimal Gini,
        long MoneySupplyCents,
        double P95TickMilliseconds,
        double ElapsedMilliseconds,
        IReadOnlyList<string> ContinuityFailures);
}

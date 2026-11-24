using System.Text.Json;
using Genelife.Api.Persistence;
using Genelife.Api.Services;
using Genelife.Family;

namespace Genelife.UnitTests.Performance;

[Trait("Category", "Performance")]
public class FamilyPerformanceTests
{
    [Fact]
    public void TwoThousandSimsStayInsideTheTickBudgetAndCandidateCap()
    {
        using var engine = Build(2_000, seed: 21);
        Run(engine, years: 1);

        var status = engine.BuildFamilyStatus();
        var metrics = engine.GetMetricsSnapshot();

        Assert.True(status.Population > 1_000, $"population collapsed to {status.Population}");
        Assert.True(metrics.P95TickMilliseconds <= 15,
            $"tick p95 {metrics.P95TickMilliseconds:F2}ms exceeds 15ms at 2,000 sims");
        Assert.True(status.Performance.CandidateMax <= status.Performance.CandidateCap,
            $"candidate max {status.Performance.CandidateMax} exceeds cap {status.Performance.CandidateCap}");
        Assert.True(status.Performance.CandidateAverage <= status.Performance.CandidateCap);
    }

    [Fact]
    public void FiveThousandSimSmokeDoesNotThrow()
    {
        using var engine = Build(5_000, seed: 7);
        for (var i = 0; i < 200; i++)
            engine.Tick(1f / 20f);

        Assert.True(engine.BuildFamilyStatus().Population > 2_000);
    }

    [Fact]
    public void CandidateCapHoldsForADecadeAtTwoHundredSims()
    {
        using var engine = Build(200, seed: 21);
        Run(engine, years: 10);

        var status = engine.BuildFamilyStatus();
        Assert.True(status.Performance.CandidateMax <= status.Performance.CandidateCap);
        Assert.True(status.Performance.CandidateSims <= status.Population);
        Assert.Empty(RelationshipInvariants.Verify(engine.World!, engine.Relationships));
    }

    [Fact]
    public void RestoreAndContinueDoesNotGrowWithoutBound()
    {
        using var engine = Build(200, seed: 21);
        Run(engine, years: 1);
        var state = engine.CaptureState(engine.NetworkIds);
        engine.Shutdown();

        using var restored = new SimulationEngine();
        restored.Restore(state, restored.NetworkIds);
        var start = restored.BuildFamilyStatus().Population;
        for (var i = 0; i < 1_000; i++)
            restored.Tick(1f / 20f);

        var end = restored.BuildFamilyStatus().Population;
        Assert.InRange(end, start / 2, start * 2);
        Assert.Empty(RelationshipInvariants.Verify(restored.World!, restored.Relationships));
        Assert.DoesNotContain(
            ContinuityInvariants.Verify(restored.World!, restored.Relationships, restored.Economy!),
            issue => issue.Level == "Fail");
    }

    [Fact]
    public void SnapshotSizeAtTwoThousandSimsStaysWithinBudget()
    {
        using var engine = Build(2_000, seed: 21);
        Run(engine, years: 1);
        var state = engine.CaptureState(engine.NetworkIds);
        var json = JsonSerializer.Serialize(state);

        Assert.True(json.Length < 50_000_000,
            $"snapshot is {json.Length:N0} bytes at 2,000 sims");
        Assert.True(state.Relationships.Count >= 0);
    }

    private static SimulationEngine Build(int sims, int seed)
    {
        var engine = new SimulationEngine();
        engine.Initialize();
        engine.ExecuteWithWorld(world =>
        {
            var generator = new PopulationGenerator(
                world, engine.NetworkIds, engine.Economy!, engine.EmploymentIndex!,
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
            engine.Tick(1f / 20f);
    }
}

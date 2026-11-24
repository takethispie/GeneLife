using Genelife.Api.Services;
using Genelife.Family;
using Genelife.Map;

namespace Genelife.UnitTests.Performance;

/// <summary>
/// Release evidence for enabling animated commutes by default. The same 2,000-sim
/// year is measured with the previous abstract commute and with animation; the
/// default is accepted only while the animated tick stays inside the budget and
/// does not materially regress against the abstract baseline.
/// </summary>
[Trait("Category", "Performance")]
public class CommuteAnimationBudgetTests
{
    [Fact]
    public void AnimatedCommutesStayInsideTheTwoThousandSimTickBudget()
    {
        var abstractMetrics = Measure(CommuteMode.Abstract);
        var animatedMetrics = Measure(CommuteMode.Animate);

        Assert.True(animatedMetrics.P95TickMilliseconds <= 15,
            $"animated commute p95 {animatedMetrics.P95TickMilliseconds:F2}ms exceeds 15ms");
        Assert.True(animatedMetrics.P95TickMilliseconds <= abstractMetrics.P95TickMilliseconds + 2.0,
            $"animated commute p95 {animatedMetrics.P95TickMilliseconds:F2}ms regresses more than 2ms " +
            $"against abstract {abstractMetrics.P95TickMilliseconds:F2}ms");
    }

    private static MetricsSnapshot Measure(CommuteMode mode)
    {
        using var engine = new SimulationEngine
        {
            CommuteOptions = new CommuteOptions { Mode = mode }
        };
        engine.Initialize();
        engine.ExecuteWithWorld(world =>
        {
            var generator = new PopulationGenerator(
                world, engine.NetworkIds, engine.Economy!, engine.EmploymentIndex!,
                relationshipIndex: engine.Relationships,
                relationshipService: engine.RelationshipService!,
                relationshipOptions: engine.RelationshipOptions);
            generator.Generate(2_000, seed: 21);
            return 0;
        });

        var ticks = engine.Economy!.Clock.TicksPerYear;
        for (var i = 0; i < ticks; i++)
            engine.Tick(1f / 20f);

        return engine.GetMetricsSnapshot();
    }
}

using Arch.Core;
using Genelife.Api.Services;
using Genelife.Components.Family;
using Genelife.Components.Life;
using Genelife.Family;
using Genelife.Values;

namespace Genelife.UnitTests.Family;

public class ReproductionAcceptanceTests
{
    [Fact]
    public void SeededWorldProducesBirthsWithCompleteChildrenAndNoInvariantFailures()
    {
        using var engine = new SimulationEngine();
        engine.Initialize(
            relationshipOptions: new RelationshipOptions { SeedPartnershipShare = 0.7m },
            reproductionOptions: new ReproductionOptions
            {
                MonthlyConceptionChance = 0.1m,
                MonthlyLossChance = 0m,
                PregnancyMonths = 3,
                MonthlyPrenatalCareCost = Money.FromUnits(10m)
            });

        engine.ExecuteWithWorld(world =>
        {
            var generator = new PopulationGenerator(
                world, engine.NetworkIds, engine.Economy!, engine.EmploymentIndex!,
                relationshipIndex: engine.Relationships,
                relationshipService: engine.RelationshipService!,
                relationshipOptions: engine.RelationshipOptions);
            generator.Generate(80, seed: 21);
            return 0;
        });

        var ticks = 2 * engine.Economy!.Clock.TicksPerYear;
        for (var i = 0; i < ticks; i++)
            engine.Tick(1f / 20f);

        Assert.True(engine.BuildPregnancies().Count > 0 || engine.BuildChildren()
            .Any(child => child.Age == 0), "expected at least one pregnancy or newborn");
        Assert.Contains(engine.BuildChildren(), child => child.Age < 3);

        var issues = engine.BuildFamilyStatus().Health;
        Assert.DoesNotContain(issues, issue => issue.Level == "Fail");
        Assert.Empty(RelationshipInvariants.Verify(engine.World!, engine.Relationships));
        Assert.DoesNotContain(
            ReproductionInvariants.Verify(engine.World!, engine.Relationships, engine.Economy!),
            issue => issue.Level == "Fail");
    }

    [Fact]
    public void ChildCostsAndBirthsKeepMoneySupplyStable()
    {
        using var engine = new SimulationEngine();
        engine.Initialize(
            reproductionOptions: new ReproductionOptions
            {
                MonthlyConceptionChance = 0.1m,
                MonthlyLossChance = 0m,
                PregnancyMonths = 2
            });
        engine.ExecuteWithWorld(world =>
        {
            var generator = new PopulationGenerator(
                world, engine.NetworkIds, engine.Economy!, engine.EmploymentIndex!,
                relationshipIndex: engine.Relationships,
                relationshipService: engine.RelationshipService!,
                relationshipOptions: engine.RelationshipOptions);
            generator.Generate(60, seed: 22);
            return 0;
        });

        var before = engine.Economy!.TotalMoneySupply();
        var sourceBefore = engine.Economy.Ledger.SourceTotal;
        var sinkBefore = engine.Economy.Ledger.SinkTotal;
        var ticks = engine.Economy.Clock.TicksPerYear;
        for (var i = 0; i < ticks; i++)
            engine.Tick(1f / 20f);
        var after = engine.Economy.TotalMoneySupply();
        var growth = before.Cents > 0
            ? (after.Cents - before.Cents) / (decimal)before.Cents
            : 0m;
        var conservationGap = (after - before) -
                              ((engine.Economy.Ledger.SourceTotal - sourceBefore) -
                               (engine.Economy.Ledger.SinkTotal - sinkBefore));

        Assert.InRange(growth, 0m, 0.05m);
        Assert.Equal(Money.Zero, conservationGap);
    }
}

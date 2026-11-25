using Genelife.Api.Commands;
using Genelife.Api.Repositories;
using Genelife.Api.Services;
using Genelife.Family;

namespace Genelife.UnitTests.Family;

public class FamilyAcceptanceTests
{
    [Fact]
    public void TraitSeedProducesACompleteDistribution()
    {
        using var engine = Build(100, seed: 99);
        var status = engine.BuildFamilyStatus();

        Assert.Equal(100, status.Population);
        Assert.Equal(100, status.Traits.WithTraits);
        Assert.Equal(0, status.Traits.MissingTraits);
        Assert.Equal(10, status.Traits.Categories.Count);
        Assert.InRange(status.Traits.FemaleShare, 0.35m, 0.65m);
        Assert.All(status.Traits.Categories, category => Assert.NotEmpty(category.Values));
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void FamilyDecadeHasNoFailHealthAndKeepsTheMoneyLoopPositive()
    {
        using var engine = Build(200, seed: 21);
        var before = engine.Economy!.TotalMoneySupply();
        Run(engine, years: 10);
        var after = engine.Economy!.TotalMoneySupply();

        var status = engine.BuildFamilyStatus();
        Assert.True(status.Population > 0);
        Assert.True(after.IsPositive);
        Assert.True(after >= before);
        Assert.DoesNotContain(status.Health, check => check.Level == "Fail");
        Assert.Empty(RelationshipInvariants.Verify(engine.World!, engine.Relationships));
        Assert.DoesNotContain(
            ContinuityInvariants.Verify(engine.World!, engine.Relationships, engine.Economy!),
            issue => issue.Level == "Fail");
    }

    [Fact]
    public void AdminOperationsRejectUnknownTargets()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        engine.ExecuteWithWorld(world =>
        {
            var repository = new GenosRepository(world, engine.NetworkIds, engine.Economy);
            repository.Add("First", 30);
            repository.Add("Second", 31);
            return 0;
        });

        var cancel = engine.EnqueueAdminCommand(new CancelPregnancyCommand(
            Guid.NewGuid(), "tester", 9_999_999, "missing"));
        var trust = engine.EnqueueAdminCommand(new ReleaseTrustCommand(
            Guid.NewGuid(), "tester", 9_999_999, "missing"));
        var guardian = engine.EnqueueAdminCommand(new AssignGuardianCommand(
            Guid.NewGuid(), "tester", 9_999_999, 9_999_998, "missing"));
        engine.Tick(1f / 20f);

        foreach (var commandId in new[] { cancel, trust, guardian })
        {
            var result = engine.GetCommandResult(commandId);
            Assert.NotNull(result);
            Assert.False(result!.Accepted);
            Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static SimulationEngine Build(int genoses, int seed)
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
            generator.Generate(genoses, seed: seed);
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

using Genelife.Api.Persistence;
using Genelife.Api.Services;
using Genelife.Family;

namespace Genelife.UnitTests.Family;

[Trait("Category", "Scenario")]
public class ReplayTests
{
    [Fact]
    public void DecadeReplayMatchesTraitsGraphBirthsAndMoney()
    {
        var first = RunAndCapture(sims: 100, seed: 21, years: 10);
        var second = RunAndCapture(sims: 100, seed: 21, years: 10);

        AssertSame(first, second);
    }

    [Fact]
    public void DifferentSeedProducesDifferentFamilyFacts()
    {
        var first = RunAndCapture(sims: 60, seed: 21, years: 2);
        var different = RunAndCapture(sims: 60, seed: 22, years: 2);

        Assert.NotEqual(first.Traits, different.Traits);
        Assert.NotEqual(first.Relationships, different.Relationships);
    }

    [Fact]
    public void SeedSweepHasNoInvariantFailureOrCollapse()
    {
        foreach (var seed in new[] { 11, 23, 37, 51, 73 })
        {
            using var engine = Build(sims: 60, seed: seed);
            Run(engine, years: 2);

            Assert.True(engine.BuildFamilyStatus().Population > 0, $"seed {seed} collapsed");
            Assert.Empty(RelationshipInvariants.Verify(engine.World!, engine.Relationships));
            Assert.DoesNotContain(
                ReproductionInvariants.Verify(engine.World!, engine.Relationships, engine.Economy!),
                issue => issue.Level == "Fail");
            Assert.DoesNotContain(
                ContinuityInvariants.Verify(engine.World!, engine.Relationships, engine.Economy!),
                issue => issue.Level == "Fail");
        }
    }

    [Fact]
    public void LegacyMigrationReplayMatchesADoubleRestore()
    {
        using var source = Build(sims: 60, seed: 21);
        var state = source.CaptureState(source.NetworkIds);
        source.Shutdown();

        state.Relationships.Clear();
        state.Pregnancies.Clear();
        state.Trusts.Clear();
        state.StateCare.Clear();
        foreach (var sim in state.Sims)
            sim.TraitsMigrated = false;
        state.SchemaVersion = 5;
        SnapshotMigrations.Upgrade(state, 5);

        using var first = new SimulationEngine();
        first.Restore(state, first.NetworkIds);
        Assert.Empty(first.BuildFamilyRelationships().Relationships);
        Run(first, years: 1);
        var firstCapture = ReplayCapture.Capture(first);

        using var second = new SimulationEngine();
        second.Restore(state, second.NetworkIds);
        Run(second, years: 1);
        var secondCapture = ReplayCapture.Capture(second);

        AssertSame(firstCapture, secondCapture);
    }

    [Fact]
    public void FamilyCodeHasNoRandomSharedOrWallClockDependency()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "Genelife.Domain", "Family"), "*.cs")
            .Concat(Directory.GetFiles(Path.Combine(root, "Genelife.Domain", "Components", "Family"), "*.cs"))
            .ToList();

        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("Random.Shared", text, StringComparison.Ordinal);
            Assert.DoesNotContain("DateTime.Now", text, StringComparison.Ordinal);
            Assert.DoesNotContain("DateTime.UtcNow", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Stopwatch", text, StringComparison.Ordinal);
        }
    }

    private static ReplayCapture RunAndCapture(int sims, int seed, int years)
    {
        using var engine = Build(sims, seed);
        Run(engine, years);
        return ReplayCapture.Capture(engine);
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

    private static void AssertSame(ReplayCapture expected, ReplayCapture actual)
    {
        Assert.Equal(expected.Traits, actual.Traits);
        Assert.Equal(expected.Relationships, actual.Relationships);
        Assert.Equal(expected.Pregnancies, actual.Pregnancies);
        Assert.Equal(expected.Households, actual.Households);
        Assert.Equal(expected.Population, actual.Population);
        Assert.Equal(expected.MoneyCents, actual.MoneyCents);
        Assert.Equal(expected.LedgerSourceCents, actual.LedgerSourceCents);
        Assert.Equal(expected.LedgerSinkCents, actual.LedgerSinkCents);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "GeneLife.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}

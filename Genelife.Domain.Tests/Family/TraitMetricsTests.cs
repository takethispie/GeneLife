using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Family;

namespace Genelife.UnitTests.Family;

public class TraitMetricsTests
{
    [Fact]
    public void MissingTraitsOnLivingGenosIsAFailure()
    {
        var world = World.Create();
        world.Create(new Alive(30));
        world.Create(new Alive(40), new Traits());

        var issues = TraitInvariants.Verify(world);

        Assert.Contains(issues, issue => issue.Name == "traits.missing" && issue.Level == "Fail");
    }

    [Fact]
    public void UndefinedEnumIsAFailure()
    {
        var world = World.Create();
        world.Create(new Alive(30), new Traits { Temperament = (Temperament)200 });

        var issues = TraitInvariants.Verify(world);

        Assert.Contains(issues, issue => issue.Name == "traits.invalid" && issue.Level == "Fail");
    }

    [Fact]
    public void DistributionCountsSumToTheLivingPopulationPerTrait()
    {
        var world = World.Create();
        for (var i = 0; i < 5; i++)
            world.Create(new Alive(20 + i), new Traits
            {
                EyeColor = (EyeColor)(i % 5),
                HairColor = (HairColor)(i % 5),
                Handedness = (Handedness)(i % 3),
                Morphotype = (Morphotype)(i % 3),
                Aptitude = (Aptitude)(i % 5),
                HeightPotential = (HeightPotential)(i % 3),
                Sex = (Sex)(i % 2),
                Temperament = (Temperament)(i % 3)
            });

        var distribution = TraitDistribution.Calculate(world);

        Assert.Equal(5, distribution.Population);
        Assert.Equal(5, distribution.WithTraits);
        Assert.Equal(0, distribution.MissingTraits);
        foreach (var (_, counts) in distribution.Counts)
            Assert.Equal(5, counts.Values.Sum());
        Assert.InRange(distribution.FemaleShare, 0.3m, 0.7m);
    }

    [Fact]
    public void PopulationWithoutTraitsYieldsAWarning()
    {
        var world = World.Create();
        world.Create(new Alive(30));
        world.Create(new Alive(40));

        var issues = TraitInvariants.Verify(world);

        Assert.Contains(issues, issue => issue.Name == "traits.none" && issue.Level == "Warn");
    }

    [Fact]
    public void DeadGenosesAreExcludedFromTheLivingPopulation()
    {
        var world = World.Create();
        world.Create(new Alive(30), new Traits());
        world.Create(new Alive(80), new Traits(), new Dead());

        var distribution = TraitDistribution.Calculate(world);

        Assert.Equal(1, distribution.Population);
        Assert.Equal(1, distribution.WithTraits);
    }
}

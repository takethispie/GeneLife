using Genelife.Components.Gen;
using Genelife.Family;

namespace Genelife.UnitTests.Family;

public class TraitInheritanceTests
{
    private static readonly Traits ParentA = new()
    {
        EyeColor = EyeColor.Brown,
        HairColor = HairColor.Black,
        Handedness = Handedness.Right,
        Morphotype = Morphotype.Mesomorph,
        Aptitude = Aptitude.Science,
        HeightPotential = HeightPotential.Average,
        Sex = Sex.Female,
        Temperament = Temperament.Peaceful
    };

    private static readonly Traits ParentB = new()
    {
        EyeColor = EyeColor.Blue,
        HairColor = HairColor.Blonde,
        Handedness = Handedness.Left,
        Morphotype = Morphotype.Ectomorph,
        Aptitude = Aptitude.Art,
        HeightPotential = HeightPotential.Tall,
        Sex = Sex.Male,
        Temperament = Temperament.Neutral
    };

    [Fact]
    public void ZeroMutationEachTraitEqualsOneParent()
    {
        var options = new GeneticsOptions { MutationRate = 0m };

        for (ulong childId = 1; childId <= 500; childId++)
        {
            var result = TraitsInheritance.CreateDetailed(
                ParentA, 10, ParentB, 20, 777, childId, options);

            Assert.Equal(0, result.Mutations);
            Assert.Contains(result.Traits.EyeColor, new[] { ParentA.EyeColor, ParentB.EyeColor });
            Assert.Contains(result.Traits.HairColor, new[] { ParentA.HairColor, ParentB.HairColor });
            Assert.Contains(result.Traits.Handedness, new[] { ParentA.Handedness, ParentB.Handedness });
            Assert.Contains(result.Traits.Morphotype, new[] { ParentA.Morphotype, ParentB.Morphotype });
            Assert.Contains(result.Traits.Aptitude, new[] { ParentA.Aptitude, ParentB.Aptitude });
            Assert.Contains(result.Traits.HeightPotential, new[] { ParentA.HeightPotential, ParentB.HeightPotential });
            Assert.Contains(result.Traits.Sex, new[] { ParentA.Sex, ParentB.Sex });
            Assert.Contains(result.Traits.Temperament, new[] { ParentA.Temperament, ParentB.Temperament });
        }
    }

    [Fact]
    public void MutationRateIsObservedWithinTolerance()
    {
        const int children = 10_000;
        const int traitCount = 8;
        var options = new GeneticsOptions { MutationRate = 0.04m };
        var mutations = 0;

        for (ulong childId = 1; childId <= children; childId++)
        {
            var result = TraitsInheritance.CreateDetailed(
                ParentA, 10, ParentB, 20, 123, childId, options);
            mutations += result.Mutations;
            Assert.True(TraitValidation.IsValid(result.Traits));
        }

        var rate = mutations / (decimal)(children * traitCount);
        Assert.InRange(rate, 0.025m, 0.055m);
    }

    [Fact]
    public void SameChildReplaysIdentically()
    {
        var options = new GeneticsOptions { MutationRate = 0.5m };
        var first = TraitsInheritance.CreateDetailed(ParentA, 1, ParentB, 2, 5, 900, options);
        var second = TraitsInheritance.CreateDetailed(ParentA, 1, ParentB, 2, 5, 900, options);

        Assert.Equal(first.Traits, second.Traits);
        Assert.Equal(first.Mutations, second.Mutations);
    }

    [Fact]
    public void ParentOrderDoesNotChangeTheCanonicalResult()
    {
        var options = new GeneticsOptions { MutationRate = 0.2m };
        var forward = TraitsInheritance.CreateDetailed(ParentA, 10, ParentB, 20, 33, 44, options);
        var reversed = TraitsInheritance.CreateDetailed(ParentB, 20, ParentA, 10, 33, 44, options);

        Assert.Equal(forward.Traits, reversed.Traits);
        Assert.Equal(forward.Mutations, reversed.Mutations);
    }

    [Fact]
    public void MissingParentFallsBackToAvailableParent()
    {
        var options = new GeneticsOptions { MutationRate = 0m };
        var result = TraitsInheritance.CreateWithOptionalParents(
            null, null, ParentB, 5, 11, 12, options);

        Assert.Equal(ParentB, result.Traits);
        Assert.Equal(0, result.Mutations);
    }

    [Fact]
    public void NoParentsGenerateDeterministicSeedTraits()
    {
        var options = new GeneticsOptions();
        var expected = TraitsGenerator.Create(11, 12, TraitSource.Migration, options);
        var result = TraitsInheritance.CreateWithOptionalParents(
            null, null, null, null, 11, 12, options);

        Assert.Equal(expected, result.Traits);
        Assert.Equal(0, result.Mutations);
    }
}

using Genelife.Components.Gen;
using Genelife.Family;

namespace Genelife.UnitTests.Family;

public class AttractionScorerTests
{
    private static readonly Traits First = new()
    {
        EyeColor = EyeColor.Brown,
        HairColor = HairColor.Black,
        Morphotype = Morphotype.Mesomorph,
        Aptitude = Aptitude.Science,
        HeightPotential = HeightPotential.Average,
        Temperament = Temperament.Peaceful
    };

    private static readonly Traits Second = new()
    {
        EyeColor = EyeColor.Brown,
        HairColor = HairColor.Black,
        Morphotype = Morphotype.Mesomorph,
        Aptitude = Aptitude.Science,
        HeightPotential = HeightPotential.Average,
        Temperament = Temperament.Peaceful
    };

    [Fact]
    public void ScoreIsSymmetricAndComponentsSumToTotal()
    {
        var first = Input(First, Second);
        var second = Input(Second, First);

        var forward = AttractionScorer.Score(first, EligibilityResult.Allowed);
        var reverse = AttractionScorer.Score(second, EligibilityResult.Allowed);

        Assert.True(forward.Eligible);
        Assert.Equal(forward.Score, reverse.Score);
        Assert.Equal(forward.Score, forward.Components.Sum(component => component.Points));
        Assert.Equal(
            new[]
            {
                AttractionScorer.TraitCompatibilityName,
                AttractionScorer.AgePreferenceName,
                AttractionScorer.ProximityName,
                AttractionScorer.StabilityName,
                AttractionScorer.HealthName,
                AttractionScorer.AvailabilityName
            },
            forward.Components.Select(component => component.Name));
    }

    [Fact]
    public void IneligiblePairReturnsZeroAndTheReason()
    {
        var eligibility = EligibilityResult.Rejected("same-sex-not-allowed", "Only opposite-sex pairs.");
        var result = AttractionScorer.Score(Input(First, Second), eligibility);

        Assert.False(result.Eligible);
        Assert.Equal(0, result.Score);
        Assert.Empty(result.Components);
        Assert.Equal("Only opposite-sex pairs.", result.RejectionReason);
    }

    [Fact]
    public void SimilarTraitsScoreHigherThanDifferentTraits()
    {
        var similar = Input(First, Second);
        var different = Input(First, new Traits
        {
            EyeColor = EyeColor.Hazel,
            HairColor = HairColor.Ginger,
            Morphotype = Morphotype.Endomorph,
            Aptitude = Aptitude.Physical,
            HeightPotential = HeightPotential.Short,
            Temperament = Temperament.Violent
        });

        var similarScore = AttractionScorer.Score(similar, EligibilityResult.Allowed);
        var differentScore = AttractionScorer.Score(different, EligibilityResult.Allowed);

        Assert.True(similarScore.Score > differentScore.Score);
    }

    [Fact]
    public void AgeGapAndProximityChangeTheNamedComponents()
    {
        var close = Input(First, Second, ageA: 30, ageB: 31, distance: 2);
        var far = Input(First, Second, ageA: 30, ageB: 44, distance: 100);

        var closeScore = AttractionScorer.Score(close, EligibilityResult.Allowed);
        var farScore = AttractionScorer.Score(far, EligibilityResult.Allowed);

        Assert.True(Component(closeScore, AttractionScorer.AgePreferenceName).Points >
                    Component(farScore, AttractionScorer.AgePreferenceName).Points);
        Assert.True(Component(closeScore, AttractionScorer.ProximityName).Points >
                    Component(farScore, AttractionScorer.ProximityName).Points);
    }

    [Fact]
    public void TraitDistanceIsBoundedAndSymmetric()
    {
        Assert.Equal(0, AttractionScorer.DistanceBp(EyeColor.Brown, EyeColor.Brown));
        Assert.Equal(1000, AttractionScorer.DistanceBp(EyeColor.Brown, EyeColor.Hazel));
        Assert.Equal(
            AttractionScorer.DistanceBp(EyeColor.Brown, EyeColor.Blue),
            AttractionScorer.DistanceBp(EyeColor.Blue, EyeColor.Brown));
        Assert.InRange(AttractionScorer.TraitCompatibilityBasisPoints(First, Second), 0, 1000);
    }

    private static AttractionComponent Component(AttractionResult result, string name) =>
        result.Components.Single(component => component.Name == name);

    private static AttractionInput Input(
        Traits first,
        Traits second,
        int ageA = 30,
        int ageB = 31,
        int distance = 2) =>
        new(
            1,
            2,
            first,
            second,
            ageA,
            ageB,
            SameHousehold: false,
            SameWorkplace: false,
            SameSchool: false,
            TileDistance: distance,
            IncomeA: 800m,
            IncomeB: 900m,
            EmployedA: true,
            EmployedB: true,
            HousedA: true,
            HousedB: true,
            HealthA: 90,
            HealthB: 95,
            AvailableA: true,
            AvailableB: true);
}

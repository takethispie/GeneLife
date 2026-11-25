using System.Text.Json;
using Genelife.Components.Gen;
using Genelife.Family;

namespace Genelife.UnitTests.Family;

public class AttractionCalibrationTests
{
    [Fact]
    public void DefaultCoefficientsMatchThePinnedRuntimeVersion()
    {
        var coefficients = AttractionCoefficients.Default;
        coefficients.Validate();

        Assert.Equal("v4-attraction-2", coefficients.Version);
        Assert.Equal(10_000, coefficients.ScoreScale);
        Assert.Equal(10_000, AttractionScorer.ScoreScale);
    }

    [Fact]
    public void GeneratedFixtureMatchesRuntimeCoefficients()
    {
        var root = FindRepositoryRoot();
        var fixturePath = Path.Combine(root, "Genelife.Domain/Family/attraction-coefficients.json");
        using var document = JsonDocument.Parse(File.ReadAllText(fixturePath));
        var fixture = document.RootElement;
        var coefficients = AttractionCoefficients.Default;

        Assert.Equal(coefficients.Version, fixture.GetProperty("version").GetString());
        Assert.Equal(coefficients.ScoreScale, fixture.GetProperty("scoreScale").GetInt32());

        var components = fixture.GetProperty("components");
        Assert.Equal(coefficients.TraitCompatibility, components.GetProperty("traitCompatibility").GetInt32());
        Assert.Equal(coefficients.AgePreference, components.GetProperty("agePreference").GetInt32());
        Assert.Equal(coefficients.Proximity, components.GetProperty("proximity").GetInt32());
        Assert.Equal(coefficients.Stability, components.GetProperty("stability").GetInt32());
        Assert.Equal(coefficients.Health, components.GetProperty("health").GetInt32());
        Assert.Equal(coefficients.Availability, components.GetProperty("availability").GetInt32());

        var weights = fixture.GetProperty("traitWeights");
        Assert.Equal(coefficients.EyeColorWeight, weights.GetProperty("eyeColor").GetInt32());
        Assert.Equal(coefficients.HairColorWeight, weights.GetProperty("hair").GetInt32());
        Assert.Equal(coefficients.HandednessWeight, weights.GetProperty("handedness").GetInt32());
        Assert.Equal(coefficients.MorphotypeWeight, weights.GetProperty("morphotype").GetInt32());
        Assert.Equal(coefficients.AptitudeWeight, weights.GetProperty("aptitude").GetInt32());
        Assert.Equal(coefficients.HeightPotentialWeight, weights.GetProperty("heightPotential").GetInt32());
        Assert.Equal(coefficients.TemperamentWeight, weights.GetProperty("temperament").GetInt32());
    }

    [Fact]
    public void ScoringIsIntegerAndReturnsTheComponentList()
    {
        var first = new Traits { EyeColor = EyeColor.Brown, Aptitude = Aptitude.Science };
        var second = new Traits { EyeColor = EyeColor.Brown, Aptitude = Aptitude.Science };

        var score = AttractionScorer.Score(
            first, 30, second, 31, proximityBasisPoints: 8_000,
            stabilityBasisPoints: 7_000, healthBasisPoints: 9_000, bothAvailable: true);

        Assert.InRange(score.Total, 0, AttractionScorer.ScoreScale);
        Assert.True(score.TraitCompatibility > 0);
        Assert.True(score.AgePreference > 0);
        Assert.True(score.Proximity > 0);
        Assert.True(score.Stability > 0);
        Assert.True(score.Health > 0);
        Assert.True(score.Availability > 0);
        Assert.Equal(
            score.Total,
            score.TraitCompatibility + score.AgePreference + score.Proximity +
            score.Stability + score.Health + score.Availability);
    }

    [Fact]
    public void TraitCompatibilityIsDeterministicAndSymmetric()
    {
        var first = new Traits { EyeColor = EyeColor.Brown, HeightPotential = HeightPotential.Tall };
        var second = new Traits { EyeColor = EyeColor.Brown, HeightPotential = HeightPotential.Short };

        var forward = AttractionScorer.TraitCompatibility(first, second);
        var reverse = AttractionScorer.TraitCompatibility(second, first);

        Assert.Equal(forward, reverse);
        Assert.InRange(forward, 0, AttractionCoefficients.Default.TraitCompatibility);
    }

    [Fact]
    public void RuntimeProjectsDoNotReferenceMicrosoftMl()
    {
        var root = FindRepositoryRoot();
        foreach (var project in new[]
                 {
                     "Genelife.Domain/Genelife.Domain.csproj",
                     "Genelife.Application/Genelife.Application.csproj",
                     "Genelife.API/Genelife.API.csproj",
                     "Genelife.Contracts/Genelife.Contracts.csproj"
                 })
        {
            var text = File.ReadAllText(Path.Combine(root, project));
            Assert.DoesNotContain("Microsoft.ML", text, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(
            "Microsoft.ML",
            File.ReadAllText(Path.Combine(root, "Genelife.Domain/Family/AttractionScorer.cs")),
            StringComparison.OrdinalIgnoreCase);
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

        throw new InvalidOperationException("Could not locate GeneLife.sln above the test output directory.");
    }
}

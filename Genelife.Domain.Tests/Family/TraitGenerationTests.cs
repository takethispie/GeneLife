using Genelife.Components.Gen;
using Genelife.Family;

namespace Genelife.UnitTests.Family;

public class TraitGenerationTests
{
    [Fact]
    public void SameSeedAndNetworkIdProduceIdenticalTraits()
    {
        var options = new GeneticsOptions();
        var first = TraitsGenerator.Create(1234, 99, TraitSource.Seed, options);
        var second = TraitsGenerator.Create(1234, 99, TraitSource.Seed, options);

        Assert.Equal(first, second);
        Assert.NotEqual(first, TraitsGenerator.Create(1234, 100, TraitSource.Seed, options));
    }

    [Fact]
    public void DifferentSourcesProduceDifferentSeeds()
    {
        var a = TraitsGenerator.Create(7, 42, TraitSource.Seed);
        var b = TraitsGenerator.Create(7, 42, TraitSource.Immigrant);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void DistributionRespectsConfiguredWeights()
    {
        const int samples = 10_000;
        var options = new GeneticsOptions();
        var counts = new Dictionary<(string Trait, string Value), int>();

        for (ulong networkId = 1; networkId <= samples; networkId++)
        {
            var traits = TraitsGenerator.Create(31415, networkId, TraitSource.Seed, options);
            Increment(counts, "Sex", traits.Sex.ToString());
            Increment(counts, "Height", traits.HeightPotential.ToString());
            Increment(counts, "Temperament", traits.Temperament.ToString());
            Increment(counts, "Handedness", traits.Handedness.ToString());
        }

        Assert.InRange(Share(counts, "Sex", "Female", samples), 0.45m, 0.55m);
        Assert.InRange(Share(counts, "Height", "Average", samples), 0.45m, 0.55m);
        Assert.InRange(Share(counts, "Temperament", "Peaceful", samples), 0.45m, 0.55m);
        Assert.InRange(Share(counts, "Temperament", "Violent", samples), 0.07m, 0.13m);
        Assert.InRange(Share(counts, "Handedness", "Right", samples), 0.85m, 0.91m);
    }

    [Fact]
    public void DefaultTraitsAreValid()
    {
        Assert.True(TraitValidation.IsValid(default(Traits)));
        Assert.Empty(TraitValidation.Validate(TraitsGenerator.Create(1, 1, TraitSource.Seed)));
    }

    [Fact]
    public void OptionsRejectInvalidWeights()
    {
        var negative = new GeneticsOptions { AptitudeWeights = [1, -1, 1, 1, 1] };
        Assert.Throws<ArgumentOutOfRangeException>(negative.Validate);

        var allZero = new GeneticsOptions { HeightWeights = [0, 0, 0] };
        Assert.Throws<ArgumentException>(allZero.Validate);

        var wrongLength = new GeneticsOptions { TemperamentWeights = [1, 1] };
        Assert.Throws<ArgumentException>(wrongLength.Validate);

        var badRate = new GeneticsOptions { MutationRate = 1.1m };
        Assert.Throws<ArgumentOutOfRangeException>(badRate.Validate);
    }

    private static void Increment(Dictionary<(string Trait, string Value), int> counts, string trait, string value)
    {
        var key = (trait, value);
        counts[key] = counts.TryGetValue(key, out var current) ? current + 1 : 1;
    }

    private static decimal Share(
        Dictionary<(string Trait, string Value), int> counts,
        string trait,
        string value,
        int samples)
    {
        counts.TryGetValue((trait, value), out var count);
        return count / (decimal)samples;
    }
}

using FluentAssertions;
using GeneLife.Generators;
using GeneLife.Genetic.GeneticTraits;

namespace GeneLife.Tests.Generators;

public class PersonGeneratorTests
{
    [Fact]
    public void GenerateMale()
    {
        var dude = PersonGenerator.CreatePure(Sex.Male);
        dude.Genome.Sex.Should().Be(Sex.Male);
    }
}
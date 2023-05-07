using FluentAssertions;
using GeneLife.Entities.Person;
using GeneLife.Genetic.Data;
using GeneLife.Genetic.GeneticTraits;

namespace GeneLife.Tests.Data;

public class GenomeSequencerTests
{
    [Fact]
    public void SequenceIsCorrect()
    {
        const string sequence = "aabb";
        sequence[2..].Should().Be("bb");
        sequence[..2].Should().Be("aa");
    }
    

    [Fact]
    public void GenomeSequencingShouldWork()
    {
        var gen = new Genome(10, Sex.Male, EyeColor.Blue, HairColor.Blond, Handedness.Ambidextrous,
            Morphotype.Ectomorph, Intelligence.Art, HeightPotential.Average, BehaviorPropension.Emotional, "");

        var sequence = GenomeSequencer.ToSequence(gen);
        sequence.Should().NotBeEmpty();
        sequence.Should().Be("$$bBHhaaMMXYeeJjUu#10#$$");
    }
    

    [Fact]
    public void SequenceToGenomeShouldWork()
    {
        var gen = GenomeSequencer.ToGenome("$$bBHhaaMMXYeeJjUu#10#$$");
        gen.Sex.Should().Be(Sex.Male);
        gen.EyeColor.Should().Be(EyeColor.Blue);
        gen.HairColor.Should().Be(HairColor.Blond);
        gen.Handedness.Should().Be(Handedness.Ambidextrous);
        gen.Morphotype.Should().Be(Morphotype.Ectomorph);
        gen.Intelligence.Should().Be(Intelligence.Art);
        gen.HeightPotential.Should().Be(HeightPotential.Average);
        gen.BehaviorPropension.Should().Be(BehaviorPropension.Emotional);
    }
}
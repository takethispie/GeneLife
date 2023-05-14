using FluentAssertions;
using GeneLife.Genetic;
using GeneLife.Genetic.Data;
using GeneLife.Genetic.GeneticTraits;
using GeneLife.Genetic.Mutators;

namespace GeneLife.Tests.Data;

public class GeneticTests
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
        var gen = new Genome(10, Sex.Male, EyeColor.Blue, HairColor.Blonde, Handedness.Ambidextrous,
            Morphotype.Ectomorph, Intelligence.Art, HeightPotential.Average, BehaviorPropension.Emotional);

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
        gen.HairColor.Should().Be(HairColor.Blonde);
        gen.Handedness.Should().Be(Handedness.Ambidextrous);
        gen.Morphotype.Should().Be(Morphotype.Ectomorph);
        gen.Intelligence.Should().Be(Intelligence.Art);
        gen.HeightPotential.Should().Be(HeightPotential.Average);
        gen.BehaviorPropension.Should().Be(BehaviorPropension.Emotional);
        gen.Age.Should().Be(10);
    }

    [Fact]
    public void CreateChildGenome()
    {
        var mom = GenomeSequencer.ToGenome("$$BBHhAaMmXXeeJjUU#30#$$");
        var dad = GenomeSequencer.ToGenome("$$bbHHAAMMXYEejjuu#30#$$");
        var dadGamete = MeiosisMutator.BuildGamete(dad);
        var momGamete = MeiosisMutator.BuildGamete(mom);
        var childGenome = GeneticMergingMutator.ProduceZygote(dadGamete, momGamete);
        childGenome.Should().NotBeNull();
    }
}
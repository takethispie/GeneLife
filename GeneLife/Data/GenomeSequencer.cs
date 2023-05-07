using System.Diagnostics;
using GeneLife.Data.Exceptions;
using GeneLife.GeneticTraits;

namespace GeneLife.Data;

public static class GenomeSequencer
{
    public static Genome ToGenome(string sequence)
    {
        if (!sequence.ToLower().StartsWith("$$") && !sequence.ToLower().EndsWith("$$")) 
            throw new GenomeParsingError();
        sequence = sequence[2..^2];
        IEnumerable<ChromosomePair> gen = new List<ChromosomePair>();
        while (sequence != "") (sequence, gen) = SequenceTransformStep(sequence, gen);
        var chromosomePairs = gen.ToList();
        if (gen == null || !chromosomePairs.Any()) throw new GenomeParsingError();
        gen = chromosomePairs.DistinctBy(x => x.Values);
        return new Genome(
            AgeGenome(gen.FirstOrDefault(x => x.Id == 10)),
            SexGenome(gen.FirstOrDefault(x => x.Id == 5)),
            EyeColorGenome(gen.FirstOrDefault(x => x.Id == 2)),
            HairGenome(gen.FirstOrDefault(x => x.Id == 1)),
            HandednessGenome(gen.FirstOrDefault(x => x.Id == 3)),
            MorphotypeGenome(gen.FirstOrDefault(x => x.Id == 7)),
            IntelligenceGenome(gen.FirstOrDefault(x => x.Id == 4)),
            HeightPotentialGenome(gen.FirstOrDefault(x => x.Id == 8)),
            BehaviorPropensionGenome(gen.FirstOrDefault(x => x.Id == 9))
        );
    }
    
    

    public static (string seq, IEnumerable<ChromosomePair> gen) SequenceTransformStep(string sequence, IEnumerable<ChromosomePair> gen) =>
        sequence switch
        {
            ['a' or 'A', 'a' or 'A', ..] => (sequence[2..], gen.Append(new ChromosomePair(3, sequence[..2]))),
            ['b' or 'B', 'b' or 'B', ..] => (sequence[2..], gen.Append(new ChromosomePair(1, sequence[..2]))),
            ['e' or 'E', 'e' or 'E', ..] => (sequence[2..], gen.Append(new ChromosomePair(7, sequence[..2]))),
            ['h' or 'H', 'h' or 'H', ..] => (sequence[2..], gen.Append(new ChromosomePair(2, sequence[..2]))),
            ['j' or 'J', 'j' or 'J', ..] => (sequence[2..], gen.Append(new ChromosomePair(8, sequence[..2]))),
            ['m' or 'M', 'm' or 'M', ..] => (sequence[2..], gen.Append(new ChromosomePair(4, sequence[..2]))),
            ['u' or 'U', 'u' or 'U', ..] => (sequence[2..], gen.Append(new ChromosomePair(9, sequence[..2]))),
            ['x' or 'X', 'y' or 'Y', ..] or 
            ['x' or 'X', 'x' or 'X', ..] => (sequence[2..], gen.Append(new ChromosomePair(5, sequence[..2]))),
            ['#', var a, var b, '#'] => (sequence[4..], gen.Append(new ChromosomePair(10, $"{a}{b}"))),
            _ => (sequence[1..], gen)
        };

    public static string ToSequence(Genome genome)
    {
        string sequence = $"$${HairSequence(genome)}{EyeColorSequence(genome)}{HandednessSequence(genome)}" +
                          $"{IntelligenceSequence(genome)}{SexSequence(genome)}{MorphotypeSequence(genome)}" +
                          $"{HeightPotentialSequence(genome)}{BehaviorPropensionSequence(genome)}" +
                          $"#{AgeSequence(genome).PadLeft(2, '0')}#$$";
        return sequence;
    }


    private static string HairSequence(Genome genome) => genome switch
    {
        { HairColor: HairColor.Blond } => "bB",
        { HairColor: HairColor.Ginger } => "bb",
        { HairColor: HairColor.Brown } => "BB",
        _ => throw new GenomeParsingError()
    };

    private static string EyeColorSequence(Genome genome) => genome switch
    {
        { EyeColor: EyeColor.Brown } => "HH",
        { EyeColor: EyeColor.Blue } => "Hh",
        { EyeColor: EyeColor.Green } => "hh",
        _ => throw new GenomeParsingError()
    };

    private static string HandednessSequence(Genome genome) => genome switch
    {
        { Handedness: Handedness.RightHanded } => "AA",
        { Handedness: Handedness.LeftHanded } => "Aa",
        { Handedness: Handedness.Ambidextrous } => "aa",
        _ => throw new GenomeParsingError()
    };

    private static string IntelligenceSequence(Genome genome) => genome switch
    {
        { Intelligence: Intelligence.Art } => "MM",
        { Intelligence: Intelligence.Science } => "mM",
        _ => throw new GenomeParsingError()
    };

    private static string SexSequence(Genome genome) => genome switch
    {
        { Sex: Sex.Male } => "XY",
        { Sex: Sex.Female } => "XX",
        _ => throw new GenomeParsingError()
    };

    private static string MorphotypeSequence(Genome genome) => genome switch
    {
        { Morphotype: Morphotype.Ectomorph } => "ee",
        { Morphotype: Morphotype.Mesomorph } => "EE",
        { Morphotype: Morphotype.Endomorph } => "Ee",
        _ => throw new GenomeParsingError()
    };

    private static string HeightPotentialSequence(Genome genome) => genome switch
    {
        { HeightPotential: HeightPotential.Short } => "jj",
        { HeightPotential: HeightPotential.Average } => "Jj",
        { HeightPotential: HeightPotential.Tall } => "JJ",
        _ => throw new GenomeParsingError()
    };

    private static string BehaviorPropensionSequence(Genome genome) => genome switch
    {
        { BehaviorPropension: BehaviorPropension.Peaceful } => "uu",
        { BehaviorPropension: BehaviorPropension.Emotional } => "Uu",
        { BehaviorPropension: BehaviorPropension.Violent } => "UU",
        _ => throw new GenomeParsingError()
    };

    private static string AgeSequence(Genome genome) => genome.Age.ToString();
    
    private static HairColor HairGenome(ChromosomePair genome) => genome.Values switch
    {
        "bB" => HairColor.Blond,
        "bb" => HairColor.Ginger,
        "BB" => HairColor.Brown,
        _ => throw new GenomeParsingError()
    };

    private static EyeColor EyeColorGenome(ChromosomePair genome) => genome.Values switch
    {
        "HH" => EyeColor.Brown,
        "Hh" => EyeColor.Blue,
        "hh" => EyeColor.Green,
        _ => throw new GenomeParsingError()
    };

    private static Handedness HandednessGenome(ChromosomePair genome) => genome.Values switch
    {
        "AA" => Handedness.RightHanded,
        "Aa" => Handedness.LeftHanded,
        "aa" => Handedness.Ambidextrous,
        _ => throw new GenomeParsingError()
    };

    private static Intelligence IntelligenceGenome(ChromosomePair genome) => genome.Values switch
    {
        "MM" => Intelligence.Art,
        "mM" => Intelligence.Science,
        _ => throw new GenomeParsingError()
    };

    private static Sex SexGenome(ChromosomePair genome) => genome.Values switch
    {
        "XY" => Sex.Male,
        "XX" => Sex.Female,
        _ => throw new GenomeParsingError()
    };

    private static Morphotype MorphotypeGenome(ChromosomePair genome) => genome.Values switch
    {
        "ee" => Morphotype.Ectomorph,
        "EE" => Morphotype.Mesomorph,
        "Ee" => Morphotype.Endomorph,
        _ => throw new GenomeParsingError()
    };

    private static HeightPotential HeightPotentialGenome(ChromosomePair genome) => genome.Values switch
    {
        "jj" => HeightPotential.Short,
        "Jj" => HeightPotential.Average,
        "JJ" => HeightPotential.Tall,
        _ => throw new GenomeParsingError()
    };

    private static BehaviorPropension BehaviorPropensionGenome(ChromosomePair genome) => genome.Values switch
    {
        "uu" => BehaviorPropension.Peaceful,
        "Uu" => BehaviorPropension.Emotional,
        "UU" => BehaviorPropension.Violent,
        _ => throw new GenomeParsingError()
    };

    private static int AgeGenome(ChromosomePair genome) => int.Parse(genome.Values);
}
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
        var gen = new Genome(0, Sex.Male, EyeColor.Blue, HairColor.Blond, Handedness.Ambidextrous, Morphotype.Ectomorph,
            Intelligence.Art, HeightPotential.Average, BehaviorPropension.Emotional);

        while (sequence != "") (sequence, gen) = SequenceTransformStep(sequence, gen);
        return gen;
    }

    public static (string seq, Genome gen) SequenceTransformStep(string sequence, Genome gen) =>
        sequence.ToLower() switch
        {
            ['b', 'b', ..] => (sequence[2..], gen with { HairColor = HairGenome(sequence[..2])}),
            ['h', 'h', ..] => (sequence[2..], gen with { EyeColor = EyeColorGenome(sequence[..2])}),
            ['a', 'a', ..] => (sequence[2..], gen with { Handedness = HandednessGenome(sequence[..2])}),
            ['m', 'm', ..] => (sequence[2..], gen with { Intelligence = IntelligenceGenome(sequence[..2])}),
            ['x', 'y', ..] => (sequence[2..], gen with { Sex = Sex.Male }),
            ['x', 'x', ..] => (sequence[2..], gen with { Sex = Sex.Female }),
            ['e', 'e', ..] => (sequence[2..], gen with { Morphotype = MorphotypeGenome(sequence[..2])}),
            ['j', 'j', ..] => (sequence[2..], gen with { HeightPotential = HeightPotentialGenome(sequence[..2])}),
            ['u', 'u', ..] => (sequence[2..], gen with { BehaviorPropension = BehaviorPropensionGenome(sequence[..2])}),
            ['#', var a, var b, '#'] => (sequence[4..], gen with { Age = AgeGenome($"{a}{b}")}),
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
    
    private static HairColor HairGenome(string genome) => genome switch
    {
         "bB" => HairColor.Blond,
         "bb" => HairColor.Ginger,
         "BB" => HairColor.Brown,
        _ => throw new GenomeParsingError()
    };

    private static EyeColor EyeColorGenome(string genome) => genome switch
    {
        "HH" => EyeColor.Brown,
        "Hh" => EyeColor.Blue,
        "hh" => EyeColor.Green,
        _ => throw new GenomeParsingError()
    };

    private static Handedness HandednessGenome(string genome) => genome switch
    {
        "AA" => Handedness.RightHanded,
        "Aa" => Handedness.LeftHanded,
        "aa" => Handedness.Ambidextrous,
        _ => throw new GenomeParsingError()
    };

    private static Intelligence IntelligenceGenome(string genome) => genome switch
    {
        "MM" => Intelligence.Art,
        "mM" => Intelligence.Science,
        _ => throw new GenomeParsingError()
    };

    private static Sex SexGenome(string genome) => genome switch
    {
        "XY" => Sex.Male,
        "XX" => Sex.Female,
        _ => throw new GenomeParsingError()
    };

    private static Morphotype MorphotypeGenome(string genome) => genome switch
    {
        "ee" => Morphotype.Ectomorph,
        "EE" => Morphotype.Mesomorph,
        "Ee" => Morphotype.Endomorph,
        _ => throw new GenomeParsingError()
    };

    private static HeightPotential HeightPotentialGenome(string genome) => genome switch
    {
        "jj" => HeightPotential.Short,
        "Jj" => HeightPotential.Average,
        "JJ" => HeightPotential.Tall,
        _ => throw new GenomeParsingError()
    };

    private static BehaviorPropension BehaviorPropensionGenome(string genome) => genome switch
    {
        "uu" => BehaviorPropension.Peaceful,
        "Uu" => BehaviorPropension.Emotional,
        "UU" => BehaviorPropension.Violent,
        _ => throw new GenomeParsingError()
    };

    private static int AgeGenome(string gene) => int.Parse(gene);
}
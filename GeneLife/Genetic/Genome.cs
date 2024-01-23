using GeneLife.Genetic.GeneticTraits;

namespace GeneLife.Genetic
{
    public record Genome(
        int Age,
        Sex Sex,
        EyeColor EyeColor,
        HairColor HairColor,
        Handedness Handedness,
        Morphotype Morphotype,
        Intelligence Intelligence,
        HeightPotential HeightPotential,
        BehaviorPropension BehaviorPropension
    );
}
using GeneLife.GeneticTraits;

namespace GeneLife;

public record Genome (
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
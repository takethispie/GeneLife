using GeneLife.Genetic.GeneticTraits;

namespace GeneLife.Entities.Person;

public record Genome (
    int Age,
    Sex Sex,
    EyeColor EyeColor,
    HairColor HairColor,
    Handedness Handedness,
    Morphotype Morphotype,
    Intelligence Intelligence,
    HeightPotential HeightPotential,
    BehaviorPropension BehaviorPropension,
    string Sequence
);
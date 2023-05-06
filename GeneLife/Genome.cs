using GeneLife.GeneticTraits;

namespace GeneLife;

public class Genome {
    public int Age { get; init; }
    public Sex Sex { get; init; }
    public EyeColor EyeColor { get; init; }
    public HairColor HairColor { get; init; }
    public Handedness Handedness { get; init; }
    public Morphotype Morphotype { get; init; }
    public Intelligence Intelligence { get; init; }
    public HeightPotential HeightPotential { get; init; }
    public BehaviorPropension BehaviorPropension { get; init; }
}
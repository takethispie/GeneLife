using Arch.Core.Utils;
using GeneLife.CommonComponents;
using GeneLife.CommonComponents.Alcohol;
using GeneLife.CommonComponents.Environment;
using GeneLife.CommonComponents.Utils;
using GeneLife.Genetic;

namespace GeneLife.Entities;

public static class NpcArchetypeFactory
{
    public static ComponentType[] PersonArchetype() => new ComponentType[]
    {
        typeof(Identity), 
        typeof(Genome), 
        typeof(EnvironmentalTraits), 
        typeof(Living),
        typeof(Lifespan),
        typeof(Flammable),
        typeof(Movable),
        typeof(AlcoholAbsorber)
    };
}
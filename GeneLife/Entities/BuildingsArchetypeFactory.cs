using Arch.Core.Utils;
using GeneLife.CommonComponents;
using GeneLife.CommonComponents.Containers;
using GeneLife.CommonComponents.Utils;
using GeneLife.Sybil.Core;

namespace GeneLife.Entities;

public static class BuildingsArchetypeFactory
{
    public static ComponentType[] House() => new ComponentType[]
    {
        typeof(LivingBeingContainer),
        typeof(Flammable),
        typeof(Lifespan),
        typeof(Ownable),
        typeof(Adress)
    };

    public static ComponentType[] SchoolBuilding() => new ComponentType[]
    {
        typeof(LivingBeingContainer),
        typeof(Flammable),
        typeof(Adress),
        typeof(Lifespan),
        typeof(Ownable),
        typeof(School)
    };
}
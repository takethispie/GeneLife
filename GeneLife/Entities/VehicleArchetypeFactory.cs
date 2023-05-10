using Arch.Core.Utils;
using GeneLife.CommonComponents;
using GeneLife.CommonComponents.Containers;

namespace GeneLife.Entities;

public class VehicleArchetypeFactory
{
    public static ComponentType[] CarArchetype() => new ComponentType[]
    {
        typeof(Movable),
        typeof(Flammable),
        typeof(LivingBeingContainer),
        typeof(Lifespan),
        typeof(LiquidContainer),
        typeof(Ownable),
        typeof(ItemContainer)
    };
}
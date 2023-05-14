using Arch.Core.Utils;
using GeneLife.CommonComponents;
using GeneLife.CommonComponents.Containers;
using GeneLife.Entities.Exceptions;
using GeneLife.Entities.Interfaces;

namespace GeneLife.Entities.Factories;

public class VehicleArchetypeFactory : IArchetypeBuilder
{
    private static ComponentType[] CarArchetype() => new ComponentType[]
    {
        typeof(Movable),
        typeof(Flammable),
        typeof(LivingBeingContainer),
        typeof(Lifespan),
        typeof(LiquidContainer),
        typeof(Ownable),
        typeof(ItemContainer)
    };

    public ComponentType[] Build(string type) => type.ToLower() switch
    {
        "car" => CarArchetype(),
        _ => throw new ArchetypeNotFoundException()
    };

    public string[] ArchetypesList() => new[] { "Car" };
}
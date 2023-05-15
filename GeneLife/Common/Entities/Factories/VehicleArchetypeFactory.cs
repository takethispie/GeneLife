using Arch.Core.Utils;
using GeneLife.Common.Components;
using GeneLife.Common.Components.Containers;
using GeneLife.Common.Entities.Exceptions;
using GeneLife.Common.Entities.Interfaces;

namespace GeneLife.Common.Entities.Factories;

public class VehicleArchetypeFactory : IArchetypeBuilder
{
    private static ComponentType[] CarArchetype() => new ComponentType[]
    {
        typeof(Moving),
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
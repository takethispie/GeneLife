using Arch.Core.Utils;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Containers;
using GeneLife.Core.Entities.Exceptions;
using GeneLife.Core.Entities.Interfaces;

namespace GeneLife.Core.Entities.Factories;

internal class VehicleArchetypeFactory : IArchetypeBuilder
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
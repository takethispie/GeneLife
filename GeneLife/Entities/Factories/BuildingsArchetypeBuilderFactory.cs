using Arch.Core.Utils;
using GeneLife.CommonComponents;
using GeneLife.CommonComponents.Containers;
using GeneLife.CommonComponents.Utils;
using GeneLife.Entities.Exceptions;
using GeneLife.Entities.Interfaces;
using GeneLife.Sibyl.Components;

namespace GeneLife.Entities.Factories;

public class BuildingsArchetypeBuilderFactory : IArchetypeBuilder
{
    private static ComponentType[] House() => new ComponentType[]
    {
        typeof(LivingBeingContainer),
        typeof(Flammable),
        typeof(Lifespan),
        typeof(Ownable),
        typeof(Adress)
    };

    private static ComponentType[] SchoolBuilding() => new ComponentType[]
    {
        typeof(LivingBeingContainer),
        typeof(Flammable),
        typeof(Adress),
        typeof(Lifespan),
        typeof(Ownable),
        typeof(School)
    };

    public ComponentType[] Build(string type)
    {
        return type.ToLower() switch
        {
            "house" => House(),
            "schoolbuilding" => SchoolBuilding(),
            _ => throw new ArchetypeNotFoundException()
        };
    }

    public string[] ArchetypesList() => new[] { "House", "SchoolBuilding" };
}
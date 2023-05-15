using Arch.Core.Utils;
using GeneLife.Common.Components;
using GeneLife.Common.Components.Containers;
using GeneLife.Common.Components.Utils;
using GeneLife.Common.Entities.Exceptions;
using GeneLife.Common.Entities.Interfaces;
using GeneLife.Sibyl.Components;

namespace GeneLife.Common.Entities.Factories;

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
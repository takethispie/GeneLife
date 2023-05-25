using Arch.Core.Utils;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Buildings;
using GeneLife.Core.Entities.Exceptions;
using GeneLife.Core.Entities.Interfaces;
using GeneLife.Sibyl.Components;

namespace GeneLife.Core.Entities.Factories;

public class BuildingsArchetypeBuilderFactory : IArchetypeBuilder
{
    private static ComponentType[] House() => new ComponentType[]
    {
        typeof(Flammable),
        typeof(Lifespan),
        typeof(Ownable),
        typeof(Position),
        typeof(Adress),
        typeof(HouseHold)
    };

    private static ComponentType[] SchoolBuilding() => new ComponentType[]
    {
        typeof(Flammable),
        typeof(Lifespan),
        typeof(Ownable),
        typeof(Position),
        typeof(Adress),
        typeof(School)
    };

    private static ComponentType[] Shop() => new ComponentType[]
    {
        typeof(Flammable),
        typeof(Lifespan),
        typeof(Ownable),
        typeof(Position),
        typeof(Adress),
        typeof(Shop)
    };

    public ComponentType[] Build(string type)
    {
        return type.ToLower() switch
        {
            "house" => House(),
            "schoolbuilding" => SchoolBuilding(),
            "shop" => Shop(),
            _ => throw new ArchetypeNotFoundException()
        };
    }

    public string[] ArchetypesList() => new[] { "House", "SchoolBuilding", "OccupiedHouse", "shop" };
}
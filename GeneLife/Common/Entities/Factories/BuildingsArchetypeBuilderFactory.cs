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
        typeof(Flammable),
        typeof(Lifespan),
        typeof(Ownable),
        typeof(Position),
        typeof(Adress),
    };
    
    private static ComponentType[] OccupiedHouse() => new ComponentType[]
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

    public ComponentType[] Build(string type)
    {
        return type.ToLower() switch
        {
            "house" => House(),
            "occupiedhouse" => OccupiedHouse(),
            "schoolbuilding" => SchoolBuilding(),
            _ => throw new ArchetypeNotFoundException()
        };
    }

    public string[] ArchetypesList() => new[] { "House", "SchoolBuilding", "OccupiedHouse" };
}
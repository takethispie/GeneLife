using Arch.Core.Utils;
using GeneLife.CommonComponents;
using GeneLife.CommonComponents.Alcohol;
using GeneLife.CommonComponents.Environment;
using GeneLife.CommonComponents.Utils;
using GeneLife.Entities.Exceptions;
using GeneLife.Entities.Interfaces;
using GeneLife.Genetic;
using GeneLife.Sibyl.Components;

namespace GeneLife.Entities.Factories;

public class NpcArchetypeFactory : IArchetypeBuilder
{
    private static ComponentType[] PersonArchetype() => new ComponentType[]
    {
        typeof(Identity), 
        typeof(Genome), 
        typeof(EnvironmentalTraits), 
        typeof(Living),
        typeof(Lifespan),
        typeof(Flammable),
        typeof(Movable),
        typeof(AlcoholAbsorber),
        typeof(KnowledgeList),
        typeof(Position)
    };

    public ComponentType[] Build(string type) => type.ToLower() switch
    {
        "person" => PersonArchetype(),
        _ => throw new ArchetypeNotFoundException()
    };

    public string[] ArchetypesList() => new[] { "Person" };
}
using Arch.Core.Utils;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Alcohol;
using GeneLife.Core.Components.Characters;
using GeneLife.Core.Entities.Exceptions;
using GeneLife.Core.Entities.Interfaces;
using GeneLife.Genetic;
using GeneLife.Sibyl.Components;

namespace GeneLife.Core.Entities.Factories;

public class NpcArchetypeFactory : IArchetypeBuilder
{
    private static ComponentType[] PersonArchetype() => new ComponentType[]
    {
        typeof(Identity), 
        typeof(Genome), 
        typeof(Psychology), 
        typeof(Living),
        typeof(Lifespan),
        typeof(Flammable),
        typeof(Moving),
        typeof(AlcoholAbsorber),
        typeof(KnowledgeList),
        typeof(Position),
        typeof(Wallet),
        typeof(Inventory)
    };

    public ComponentType[] Build(string type) => type.ToLower() switch
    {
        "person" => PersonArchetype(),
        _ => throw new ArchetypeNotFoundException()
    };

    public string[] ArchetypesList() => new[] { "Person" };
}
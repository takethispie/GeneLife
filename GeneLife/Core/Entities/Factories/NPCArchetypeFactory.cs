using Arch.Core.Utils;
using GeneLife.Athena.Components;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Alcohol;
using GeneLife.Core.Components.Characters;
using GeneLife.Core.Entities.Exceptions;
using GeneLife.Core.Entities.Interfaces;
using GeneLife.Genetic;
using GeneLife.Sibyl.Components;

namespace GeneLife.Core.Entities.Factories;

internal class NpcArchetypeFactory : IArchetypeBuilder
{
    private static ComponentType[] PersonArchetype() => new ComponentType[]
    {
        typeof(Identity), 
        typeof(Genome), 
        typeof(Psychology), 
        typeof(Living),
        typeof(Lifespan),
        typeof(Flammable),
        typeof(AlcoholAbsorber),
        typeof(KnowledgeList),
        typeof(Position),
        typeof(Wallet),
        typeof(Inventory),
        typeof(Objectives)
    };

    public ComponentType[] Build(string type) => type.ToLower() switch
    {
        "person" => PersonArchetype(),
        _ => throw new ArchetypeNotFoundException()
    };

    public string[] ArchetypesList() => new[] { "Person" };
}
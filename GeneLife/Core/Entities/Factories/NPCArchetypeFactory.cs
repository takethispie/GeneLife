using Arch.Core.Utils;
using GeneLife.Core.Components;
using GeneLife.Core.Entities.Exceptions;
using GeneLife.Core.Entities.Interfaces;
using GeneLife.Core.Planning;
using GeneLife.Genetic;
using GeneLife.Knowledge.Components;
using GeneLife.Survival.Components;

namespace GeneLife.Core.Entities.Factories;

internal class NpcArchetypeFactory : IArchetypeBuilder
{
    private static ComponentType[] PersonArchetype() => new ComponentType[]
    {
        typeof(Genome),
        typeof(Living),
        typeof(Flammable),
        typeof(KnowledgeList),
        typeof(Inventory),
        typeof(Position),
        typeof(Planner),
        typeof(Human)
    };

    public ComponentType[] Build(string type) => type.ToLower() switch
    {
        "person" => PersonArchetype(),
        _ => throw new ArchetypeNotFoundException()
    };

    public string[] ArchetypesList() => new[] { "Person" };
}
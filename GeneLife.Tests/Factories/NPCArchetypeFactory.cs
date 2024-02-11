using Arch.Core.Utils;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Characters;
using GeneLife.Core.Entities.Exceptions;
using GeneLife.Core.Entities.Interfaces;
using GeneLife.Genetic;
using GeneLife.Knowledge.Components;
using GeneLife.Survival.Components;

namespace GeneLife.Tests.Factories
{
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
}
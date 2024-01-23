using Arch.Core;
using Arch.Core.Extensions;
using Arch.System;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Characters;
using GeneLife.Genetic;
using GeneLife.Relations.Components;
using GeneLife.Relations.Services;
using GeneLife.Survival.Components;

namespace GeneLife.Relations.Systems
{
    internal sealed class LoveInterestSystem : BaseSystem<World, float>
    {
        private readonly float _interval;
        private float currentTimeCount;
        private readonly QueryDescription potentiallySingleNPCs = new QueryDescription().WithAll<Living, Position, Moving, Identity, Genome>().WithNone<Relation>();

        public LoveInterestSystem(World world, float interval = 600) : base(world)
        {
            _interval = interval;
            currentTimeCount = 0;
        }

        public override void Update(in float t)
        {
            var delta = t;
            currentTimeCount += delta;
            if (currentTimeCount < _interval) return;
            var entities = new List<Entity>();
            World.GetEntities(potentiallySingleNPCs, entities);
            RelationService.LoveLoop(entities);
        }
    }
}
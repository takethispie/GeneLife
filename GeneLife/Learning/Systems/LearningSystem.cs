using Arch.Core;
using Arch.Core.Extensions;
using Arch.System;
using GeneLife.Core.Components;
using GeneLife.Learning.Components;
using GeneLife.Learning.Services;
using GeneLife.Survival.Components;

namespace GeneLife.Learning.Systems
{
    internal sealed class LearningSystem : BaseSystem<World, float>
    {
        private QueryDescription _LearningNPCs = new QueryDescription().WithAll<Learning, Living, KnowledgeList>();

        public LearningSystem(World world) : base(world)
        {
        }

        public override void Update(in float t)
        {
            var delta = t;
            World.Query(in _LearningNPCs, (ref Learning learning, ref Living being, ref KnowledgeList knowledgeList) =>
            {
                (learning, knowledgeList) = KnowledgeService.LearningLoop(learning, knowledgeList, delta);
            });

            var entities = new List<Entity>();
            World.GetEntities(_LearningNPCs, entities);
            foreach (var entity in entities)
            {
                var learning = entity.Get<Learning>();
                if (learning.Finished) entity.Remove<Learning>();
            }
        }
    }
}
using Arch.Core;
using Arch.Core.Extensions;
using Arch.System;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Characters;
using GeneLife.Sibyl.Components;
using GeneLife.Sibyl.Services;

namespace GeneLife.Sibyl.Systems;

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
            if (being.IsDead) return;
            (learning, knowledgeList) = KnowledgeService.LearningLoop(learning, knowledgeList, delta);
        });
        
        var entities = new List<Entity>();
        World.GetEntities(_LearningNPCs, entities);
        foreach (var entity in entities)
        {
            var learning = entity.Get<Learning>();
            if (!learning.Finished && !entity.Get<Living>().IsDead) continue;
            entity.Remove<Learning>();
        }
    }
}
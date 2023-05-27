using Arch.Core;
using Arch.System;
using GeneLife.Athena.Components;
using GeneLife.Athena.Core.Objectives;
using GeneLife.Athena.Extensions;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Characters;
using GeneLife.Core.Entities;

namespace GeneLife.Athena.Systems;

public class ShoppingSystem : BaseSystem<World, float>
{
    private readonly QueryDescription entitiesWithObjectives = new();
    
    public ShoppingSystem(World world) : base(world)
    {
        entitiesWithObjectives.All = new ArchetypeFactory().Build("person");
    }

    public override void Update(in float t)
    {
        World.ParallelQuery(in entitiesWithObjectives, (ref Living living, ref Position position, ref Wallet wallet, ref Objectives objectives) =>
        {
            if (!objectives.CurrentObjectives.IsHighestPriority(typeof(BuyItem))) return;
            
        });
    }
}
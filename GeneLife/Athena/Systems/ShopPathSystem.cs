using Arch.Bus;
using Arch.Core;
using Arch.System;
using GeneLife.Athena.Components;
using GeneLife.Athena.Core.Objectives;
using GeneLife.Athena.Extensions;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Characters;
using GeneLife.Core.Entities;
using GeneLife.Core.Entities.Factories;
using GeneLife.Core.Events;
using GeneLife.Demeter.Services;

namespace GeneLife.Athena.Systems;

public class ShopPathSystem : BaseSystem<World, float>
{
    private readonly QueryDescription entitiesWithObjectives = new();
    
    public ShopPathSystem(World world, ArchetypeFactory archetypeFactory) : base(world)
    {
        entitiesWithObjectives.All = archetypeFactory.Build("person");
    }

    public override void Update(in float t)
    {
        World.ParallelQuery(in entitiesWithObjectives,
            (ref Living living, ref Position position, ref Wallet wallet, ref Objectives objectives) =>
        {
            if (!objectives.CurrentObjectives.IsHighestPriority(typeof(BuyItem))) return;
            if (objectives.CurrentObjectives.OrderByDescending(x => x.Priority).First() is not BuyItem buyItem) return;
            var (shopPos, shopAdress, valid) = ShopSearchService.NearestShopWithItem(World, buyItem.ItemId, position);
            if (!valid) return;
            EventBus.Send(new LogEvent
            {
                Message = $"new objective set: going to a shop at {shopPos.Coordinates.X}:{shopPos.Coordinates.Y}:{shopPos.Coordinates.Z}"
            });
            objectives.CurrentObjectives.SetNewHighestPriority(new MoveTo(10, shopPos.Coordinates));
        });
    }
}
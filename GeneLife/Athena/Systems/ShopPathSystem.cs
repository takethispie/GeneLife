using Arch.Bus;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.System;
using GeneLife.Athena.Components;
using GeneLife.Athena.Core.Objectives;
using GeneLife.Athena.Extensions;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Characters;
using GeneLife.Core;
using GeneLife.Core.Components.Buildings;
using GeneLife.Core.Entities.Factories;
using GeneLife.Core.Events;
using GeneLife.Demeter.Services;

namespace GeneLife.Athena.Systems;

public class ShopPathSystem : BaseSystem<World, float>
{
    private readonly QueryDescription entitiesWithObjectives = new();
    
    public ShopPathSystem(World world, ArchetypeFactory archetypeFactory) : base(world)
    {
        entitiesWithObjectives.All = archetypeFactory.Build("person").Append(typeof(Objectives)).ToArray();
    }

    public override void Update(in float t)
    {
        var shopQuery = new QueryDescription().WithAll<Shop, Adress, Position>();
        var shops = new List<Entity>();
        World.GetEntities(in shopQuery, shops);
        World.Query(in entitiesWithObjectives,
            (ref Living living, ref Position position, ref Wallet wallet, ref Objectives objectives) =>
        {
            if (!objectives.CurrentObjectives.IsHighestPriority(typeof(BuyItem))) return;
            if (objectives.CurrentObjectives.OrderByDescending(x => x.Priority).First() is not BuyItem buyItem) return;
            var closestShop = ShopSearchService.NearestShopWithItem(World, shops, buyItem.ItemId, position);
            if (!closestShop.HasValue) return;
            var shopPos = closestShop.Value.Get<Position>();
            EventBus.Send(new LogEvent
            {
                Message = $"new objective set: going to a shop at {shopPos.Coordinates.X}:{shopPos.Coordinates.Y}:{shopPos.Coordinates.Z}"
            });
            objectives.CurrentObjectives = 
                objectives.CurrentObjectives.SetNewHighestPriority(new MoveTo(10, shopPos.Coordinates)).ToArray();
        });
    }
}
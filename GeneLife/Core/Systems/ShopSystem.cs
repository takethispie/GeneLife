using Arch.Bus;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.System;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Buildings;
using GeneLife.Core.Entities.Factories;
using GeneLife.Core.Events;
using System.Numerics;
using GeneLife.Core.Entities.Generators;
using GeneLife.Services;
using GeneLife.Survival.Components;
using GeneLife.Core.Planning;
using GeneLife.Core.Data;
using GeneLife.Core.Planning.Objective;

namespace GeneLife.Core.Systems;

public class ShopSystem : BaseSystem<World, float>
{
    private readonly QueryDescription entitiesWithObjectives = new();

    public ShopSystem(World world, ArchetypeFactory archetypeFactory) : base(world)
    {
        entitiesWithObjectives.All = archetypeFactory.Build("person");
    }

    public override void Update(in float t)
    {
        var shopQuery = new QueryDescription().WithAll<Shop, Adress, Position>();
        var shops = new List<Entity>();
        World.GetEntities(in shopQuery, shops);
        World.Query(in entitiesWithObjectives,
            (ref Living living, ref Position position, ref Human human, ref Inventory inventory, ref Planner planner) =>
        {

            if (planner.GetSlot(Clock.Time) is not BuyItem item) return;
            var itemId = item.ItemId;
            var shopType = ItemGenerator.GetItemList().First(x => x.Id == itemId).ShopType;
            if (ShopSearchService.NearestShop(shops, position) is not Entity closestShop)
            {
                EventBus.Send(new LogEvent { Message = $"can't find a shop with {item.Name}" });
                return;
            }
            var shopPos = closestShop.Get<Position>();
            if (Vector3.Distance(position.Coordinates, shopPos.Coordinates) <= 2)
            {
                var shopComponent = closestShop.Get<Shop>();
                var itemWithPrice = ItemGenerator.GetItemList().Where(x => x.Id == itemId).First();
                if (inventory.Store(itemWithPrice with { }))
                    human.Money -= itemWithPrice.Price;
            }
            else
            {
                var duration = MoveService.TimeToReach(position.Coordinates, shopPos.Coordinates, Constants.WalkingSpeed);
                if (planner.SetFirstFreeSlot(new MoveTo(10, shopPos.Coordinates, TimeOnly.FromDateTime(Clock.Time), duration)))
                {
                    EventBus.Send(new LogEvent
                    {
                        Message = $"new objective set: going to a shop at {shopPos.Coordinates.X}:{shopPos.Coordinates.Y}:{shopPos.Coordinates.Z}"
                    });
                }
                else planner.AddObjectivesToWaitingList(new MoveTo(10, shopPos.Coordinates, TimeOnly.FromDateTime(Clock.Time), duration));
            }
        });
    }
}
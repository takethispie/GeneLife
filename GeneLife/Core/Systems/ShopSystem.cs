﻿using Arch.Bus;
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
using GeneLife.Core.Objective;
using GeneLife.Core.Planning;
using GeneLife.Core.Data;

namespace GeneLife.Core.Systems
{
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

                //if (objectives.CurrentObjectives.OrderByDescending(x => x.Priority).First() is not BuyItem buyItem) return;
                var slot = planner.GetSlot(Clock.Time);
                BuyItem? item = slot switch { 
                    ObjectivePlannerSlot objSlot when objSlot.Objective is BuyItem buyItem => buyItem,
                    _ => null
                };
                if (!item.HasValue) return;
                var itemId = item.Value.ItemId;
                var shopType = ItemGenerator.GetItemList().First(x => x.Id == itemId).ShopType;
                var closestShop = ShopSearchService.NearestShop(shops, position);
                if (!closestShop.HasValue)
                {
                    EventBus.Send(new LogEvent { Message = $"can't find a shop with {item.Value.Name}" });
                    return;
                }
                var shopPos = closestShop.Value.Get<Position>();
                if (Vector3.Distance(position.Coordinates, shopPos.Coordinates) <= 2)
                {
                    var shopComponent = closestShop.Value.Get<Shop>();
                    var itemWithPrice = ItemGenerator.GetItemList().Where(x => x.Id == itemId).First();
                    //TODO handle inventory management (not enough space!)
                    if (inventory.Store(itemWithPrice with { }))
                    {
                        human.Money -= itemWithPrice.Price;
                    }
                }
                else
                {
                    EventBus.Send(new LogEvent
                    {
                        Message = $"new objective set: going to a shop at {shopPos.Coordinates.X}:{shopPos.Coordinates.Y}:{shopPos.Coordinates.Z}"
                    });
                }
            });
        }
    }
}
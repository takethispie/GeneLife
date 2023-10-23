﻿using Arch.Bus;
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
using System.Numerics;
using GeneLife.Core.Items;

namespace GeneLife.Athena.Systems;

public class ShopFindingSystem : BaseSystem<World, float>
{
    private readonly QueryDescription entitiesWithObjectives = new();
    
    public ShopFindingSystem(World world, ArchetypeFactory archetypeFactory) : base(world)
    {
        entitiesWithObjectives.All = archetypeFactory.Build("person");
    }

    public override void Update(in float t)
    {
        var shopQuery = new QueryDescription().WithAll<Shop, Adress, Position>();
        var shops = new List<Entity>();
        World.GetEntities(in shopQuery, shops);
        World.Query(in entitiesWithObjectives,
            (ref Living living, ref Position position, ref Wallet wallet, ref Objectives objectives, ref Inventory inventory) =>
        {
            if (!objectives.CurrentObjectives.IsHighestPriority(typeof(BuyItem))) return;
            if (objectives.CurrentObjectives.OrderByDescending(x => x.Priority).First() is not BuyItem buyItem) return;
            var closestShop = ShopSearchService.NearestShopWithItem(shops, buyItem.ItemId, position);
            if (!closestShop.HasValue) return;
            var shopPos = closestShop.Value.Get<Position>();
            if(Vector3.Distance(position.Coordinates, shopPos.Coordinates) <= 20) {
                var shopComponent = closestShop.Value.Get<Shop>();
                var itemWithPrice = shopComponent.AvailableItems.Where(x => x.Item.Id == buyItem.ItemId).First();
                wallet.Money -= itemWithPrice.Price;
                //TODO handle inventory management (not enough space!)
                inventory.items = inventory.items.Append(itemWithPrice.Item with {}).ToArray();
            } else {
                EventBus.Send(new LogEvent
                {
                    Message = $"new objective set: going to a shop at {shopPos.Coordinates.X}:{shopPos.Coordinates.Y}:{shopPos.Coordinates.Z}"
                });
                objectives.CurrentObjectives = 
                    objectives.CurrentObjectives.SetNewHighestPriority(new MoveTo(10, shopPos.Coordinates)).ToArray();
            }
        });
    }
}
using Arch.Bus;
using Arch.Core;
using Arch.System;
using GeneLife.Athena.Components;
using GeneLife.Athena.Core.Objectives;
using GeneLife.Athena.Extensions;
using GeneLife.Core.Components.Characters;
using GeneLife.Core.Entities.Factories;
using GeneLife.Core.Events;
using GeneLife.Core.Extensions;
using GeneLife.Core.Items;

namespace GeneLife.Demeter.Systems;

internal sealed class GroceriesInventorySystem : BaseSystem<World, float>
{
    private readonly ItemWithPrice[] _itemList;
    private readonly QueryDescription _peopleQuery = new();
    private float _tickAccumulator;

    public GroceriesInventorySystem(World world, ArchetypeFactory archetypeFactory, ItemWithPrice[] itemList) : base(world)
    {
        _itemList = itemList;
        _peopleQuery.All = archetypeFactory.Build("person").Append(typeof(Objectives)).ToArray();
    }

    public override void Update(in float t)
    {
        _tickAccumulator += t;
        if(_tickAccumulator < 1) return;
        World.Query(in _peopleQuery,
            (ref Wallet wallet, ref Inventory inventory, ref Objectives objectives, ref Identity identity) =>
        {
            if (objectives.CurrentObjectives.IsHighestPriority(typeof(Eat)))
            {
                if (inventory.items.Any(x => x.Type == ItemType.Food)) return;
                var itemToBuyId = _itemList.FirstOrDefault(x => x.Item.Type == ItemType.Food).Item.Id;
                if(itemToBuyId == 0 || wallet.Money < _itemList.MinBy(x => x.Price).Price) return;
                objectives.CurrentObjectives =
                    objectives.CurrentObjectives
                        .SetNewHighestPriority(new BuyItem(10, itemToBuyId))
                        .ToArray();
                EventBus.Send(new LogEvent
                {
                    Message = $"{identity.FullName()} has set a new high priority objective: Buy Item {itemToBuyId.ToString()}"
                });
            }
            
            if (objectives.CurrentObjectives.IsHighestPriority(typeof(Drink)))
            {
                if (inventory.items.Any(x => x.Type == ItemType.Drink)) return;
                var itemToBuyId = _itemList.FirstOrDefault(x => x.Item.Type == ItemType.Drink).Item.Id;
                if(itemToBuyId == 0 || wallet.Money < _itemList.MinBy(x => x.Price).Price) return;
                objectives.CurrentObjectives = 
                    objectives.CurrentObjectives
                        .SetNewHighestPriority(new BuyItem(10, itemToBuyId))
                        .ToArray();
                EventBus.Send(new LogEvent
                {
                    Message = $"{identity.FullName()} has set a new high priority objective: Buy Item {itemToBuyId.ToString()}"
                });
            }
        });
    }
}
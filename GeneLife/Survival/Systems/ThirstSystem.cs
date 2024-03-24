using Arch.Bus;
using Arch.Core;
using Arch.System;
using GeneLife.Core.Components;
using GeneLife.Core.Data;
using GeneLife.Core.Entities.Factories;
using GeneLife.Core.Events;
using GeneLife.Core.Items;
using GeneLife.Core.Planning;
using GeneLife.Core.Planning.Objective;
using GeneLife.Survival.Components;

namespace GeneLife.Survival.Systems;

internal sealed class ThirstSystem : BaseSystem<World, float>
{
    private readonly QueryDescription livingEntities = new QueryDescription();
    private float _tickAccumulator;

    public ThirstSystem(World world, ArchetypeFactory archetypeFactory) : base(world)
    {
        livingEntities.All = archetypeFactory.Build("person");
        _tickAccumulator = 0;
    }

    public override void Update(in float delta)
    {
        _tickAccumulator += delta;
        World.Query(in livingEntities, (ref Living living, ref Human human, ref Inventory inventory, ref Planner planner) =>
        {
            if (_tickAccumulator >= Constants.TicksPerDay)
            {
                _tickAccumulator = 0;
                living.Thirst -= 1;
                switch (living)
                {
                    case { Thirsty: false } when living.Thirst <= Constants.ThirstyThreshold:
                        EventBus.Send(new LogEvent { Message = $"{human.FirstName} {human.LastName} is starting to be very Thirsty" });
                        living.Thirsty = true;
                        break;

                    case { Thirst: <= 0, Thirsty: true, Stamina: > 0 }:
                        living.Stamina -= 5;
                        EventBus.Send(new LogEvent { Message = $"{human.FirstName} {human.LastName} is Dehydrated" });
                        break;

                    case { Thirsty: true, Stamina: <= 0 }:
                        EventBus.Send(new LogEvent
                        {
                            Message = $"{human.FirstName} {human.LastName} is slowly dying from Dehydration"
                        });
                        living.Damage += 1;
                        human.EmotionalBalance -= 10;
                        break;
                }
            }

            var hasDrinkInInventory = inventory.HasItemType(ItemType.Drink);
            if (living.Thirst < Constants.ThirstyThreshold && hasDrinkInInventory)
            {
                var food = inventory.Take(ItemType.Drink);
                if (food.HasValue)
                {
                    living.Thirsty = false;
                    living.Thirst = Constants.MaxThirst;
                    human.EmotionalBalance += 50;
                    EventBus.Send(new LogEvent { Message = $"{human.FirstName} {human.LastName} is totally hydrated" });
                }
            }

            if (living.Thirst < Constants.ThirstyThreshold 
                && !hasDrinkInInventory 
                && planner.GetAllObjectivePlannerSlots().All(x => x is not BuyItem))
            {

                var slot = planner.GetFirstFreeSlot();
                if (slot == null) planner.AddObjectivesToWaitingList(new BuyItem(10, 1));
                else planner.SetSlot(new BuyItem(10, 2) { Start = slot.Start, Duration = TimeSpan.FromHours(1) });
                EventBus.Send(new LogEvent
                {
                    Message = $"{human.FirstName} {human.LastName} has set a new high priority objective: buy drink"
                });
            }
        });
    }
}
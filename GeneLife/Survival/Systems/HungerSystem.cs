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

internal sealed class HungerSystem : BaseSystem<World, float>
{
    private readonly QueryDescription livingEntities = new();
    private float _tickAccumulator;

    public HungerSystem(World world, ArchetypeFactory archetypeFactory) : base(world)
    {
        _tickAccumulator = 0;
        livingEntities.All = archetypeFactory.Build("person");
    }

    public override void Update(in float delta)
    {
        _tickAccumulator += delta;
        World.Query(in livingEntities, (ref Living living, ref Human human, ref Inventory inventory, ref Planner planner) =>
        {
            if (_tickAccumulator >= Constants.TicksPerDay)
            {
                _tickAccumulator = 0;
                living.Hunger -= 1;
                switch (living)
                {
                    case { Hungry: false } when living.Hunger <= Constants.HungryThreshold:
                        EventBus.Send(new LogEvent { Message = $"{human.FirstName} {human.LastName} is starting to be very hungry" });
                        living.Hungry = true;
                        break;

                    case { Hunger: <= 0, Hungry: true, Stamina: > 0 }:
                        living.Stamina -= 5;
                        EventBus.Send(new LogEvent { Message = $"{human.FirstName} {human.LastName} is starving" });
                        break;

                    case { Hungry: true, Stamina: <= 0 }:
                        EventBus.Send(new LogEvent
                        {
                            Message = $"{human.FirstName} {human.LastName} is slowly dying from starvation"
                        });
                        living.Damage += 1;
                        human.EmotionalBalance -= 10;
                        break;
                }
            }

            if (living.Hunger < Constants.HungryThreshold && inventory.GetItems().Any(x => x.Type == ItemType.Food))
            {
                var food = inventory.Take(ItemType.Food);
                if (food.HasValue)
                {
                    living.Hungry = false;
                    living.Hunger = Constants.MaxHunger;
                    human.EmotionalBalance += 10;
                    EventBus.Send(new LogEvent
                    {
                        Message = $"{human.FirstName} {human.LastName} has filled his/her stomach"
                    });
                }
            }            

            if (living.Hunger < Constants.HungryThreshold
                    && !inventory.HasItemType(ItemType.Food)
                    && planner.GetAllObjectivePlannerSlots().All(x => x is not BuyItem { ItemId: 1 }))
            {
                var buyItem = new BuyItem(10, 1, TimeOnly.FromDateTime(Clock.Time), 1);
                if (!planner.SetFirstFreeSlot(buyItem)) planner.AddObjectivesToWaitingList(buyItem);
                else 
                    EventBus.Send(new LogEvent
                    {
                        Message = $"{human.FirstName} {human.LastName} has set a new high priority objective: buy food"
                    });
            }
        });
    }
}
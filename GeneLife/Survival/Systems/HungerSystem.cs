using Arch.Bus;
using Arch.Core;
using Arch.System;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Characters;
using GeneLife.Core.Data;
using GeneLife.Core.Entities.Factories;
using GeneLife.Core.Events;
using GeneLife.Core.Extensions;
using GeneLife.Core.Items;
using GeneLife.Core.ObjectiveActions;
using GeneLife.Survival.Components;

namespace GeneLife.Survival.Systems
{
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
            if (_tickAccumulator >= Constants.TicksPerDay)
            {
                _tickAccumulator = 0;
                World.Query(
                    in livingEntities,
                    (ref Living living, ref Identity identity, ref Psychology psychology, ref Objectives objectives, ref Inventory inventory) =>
                {
                    living.Hunger -= 1;
                    if (living.Hunger < Constants.HungryThreshold && inventory.GetItems().Any(x => x.Type == ItemType.Food))
                    {
                        var food = inventory.Take(ItemType.Food);
                        if (food.HasValue)
                        {
                            living.Hungry = false;
                            living.Hunger = Constants.MaxHunger;
                            objectives.CurrentObjectives = objectives.CurrentObjectives.Where(x => x is not Eat).ToArray();
                            psychology.EmotionalBalance += 50;
                            psychology.Stress = 0;
                            EventBus.Send(new LogEvent
                            {
                                Message = $"{identity.FirstName} {identity.LastName} has filled his/her stomach"
                            });
                        }
                    }

                    switch (living)
                    {
                        case { Hungry: false } when living.Hunger <= Constants.HungryThreshold:
                            EventBus.Send(new LogEvent { Message = $"{identity.FirstName} {identity.LastName} is starting to be very hungry" });
                            living.Hungry = true;
                            break;

                        case { Hunger: <= 0, Hungry: true, Stamina: > 0 }:
                            living.Stamina -= 5;
                            EventBus.Send(new LogEvent { Message = $"{identity.FirstName} {identity.LastName} is starving" });
                            break;

                        case { Hungry: true, Stamina: <= 0 }:
                            EventBus.Send(new LogEvent
                            {
                                Message = $"{identity.FirstName} {identity.LastName} is slowly dying from starvation"
                            });
                            living.Damage += 1;
                            psychology.EmotionalBalance -= 20;
                            psychology.Stress += 20;
                            break;
                    }

                    if (living.Hunger < Constants.HungryThreshold
                            && !inventory.HasItemType(ItemType.Food)
                            && !objectives.CurrentObjectives.Any(x => x is BuyItem))
                    {
                        objectives.SetNewHighestPriority(new BuyItem { Priority = 10, ItemId = 1, Name = "buy some food" });
                        EventBus.Send(new LogEvent
                        {
                            Message = $"{identity.FirstName} {identity.LastName} has set a new high priority objective: buy food"
                        });
                    }
                });
            }
        }
    }
}
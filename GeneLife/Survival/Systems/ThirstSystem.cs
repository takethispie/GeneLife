using Arch.Bus;
using Arch.Core;
using Arch.System;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Characters;
using GeneLife.Core.Data;
using GeneLife.Core.Entities.Factories;
using GeneLife.Core.Events;
using GeneLife.Core.Items;
using GeneLife.Core.Objectives;
using GeneLife.Survival.Components;

namespace GeneLife.Survival.Systems
{
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
            if (_tickAccumulator < Constants.TicksPerDay) return;
            _tickAccumulator = 0;
            World.Query(in livingEntities, (ref Living living, ref Identity identity, ref Psychology psychology, ref Objectives objectives, ref Inventory inventory) =>
            {
                living.Thirst -= 1;
                var hasDrinkInInventory = inventory.HasItemType(ItemType.Drink);
                if (living.Thirst < Constants.ThirstyThreshold && hasDrinkInInventory)
                {
                    var food = inventory.Take(ItemType.Drink);
                    if (food.HasValue)
                    {
                        living.Thirsty = false;
                        living.Thirst = Constants.MaxThirst;
                        psychology.EmotionalBalance += 50;
                        psychology.Stress = 0;
                        objectives.CurrentObjectives = objectives.CurrentObjectives.Where(x => x is not Drink).ToArray();
                        EventBus.Send(new LogEvent { Message = $"{identity.FirstName} {identity.LastName} is totally hydrated" });
                    }
                }

                switch (living)
                {
                    case { Thirsty: false } when living.Thirst <= Constants.ThirstyThreshold:
                        EventBus.Send(new LogEvent { Message = $"{identity.FirstName} {identity.LastName} is starting to be very Thirsty" });
                        living.Thirsty = true;
                        break;

                    case { Thirst: <= 0, Thirsty: true, Stamina: > 0 }:
                        living.Stamina -= 5;
                        EventBus.Send(new LogEvent { Message = $"{identity.FirstName} {identity.LastName} is Dehydrated" });
                        break;

                    case { Thirsty: true, Stamina: <= 0 }:
                        EventBus.Send(new LogEvent
                        {
                            Message = $"{identity.FirstName} {identity.LastName} is slowly dying from Dehydration"
                        });
                        living.Damage += 1;
                        psychology.EmotionalBalance -= 20;
                        psychology.Stress += 20;
                        break;
                }

                if (living.Thirst < Constants.ThirstyThreshold && !hasDrinkInInventory
                    && !objectives.CurrentObjectives.Any(x => x is BuyItem))
                {

                    objectives.SetNewHighestPriority(new BuyItem { Priority = 10, ItemId = 2, Name = "Buy a drink" });
                    EventBus.Send(new LogEvent
                    {
                        Message = $"{identity.FirstName} {identity.LastName} has set a new high priority objective: buy drink"
                    });
                }
            });
        }
    }
}
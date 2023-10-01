using Arch.Bus;
using Arch.Core;
using Arch.System;
using GeneLife.Athena.Components;
using GeneLife.Athena.Core.Objectives;
using GeneLife.Athena.Extensions;
using GeneLife.Core.Components.Characters;
using GeneLife.Core.Data;
using GeneLife.Core.Entities.Factories;
using GeneLife.Core.Events;
using GeneLife.Core.Items;

namespace GeneLife.Demeter.Systems;

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
        //TODO move accumulator to accumulators component or living component
        if (_tickAccumulator < Constants.TicksPerDay) return;
        _tickAccumulator = 0;
        World.ParallelQuery(in livingEntities, (ref Living living, ref Identity identity, ref Psychology psychology, ref Objectives objectives, ref Inventory inventory) =>
        {
            living.Thirst -= 1;
            var hasDrinkInInventory = inventory.items.Any(x => x.Type == ItemType.Drink);
            if (living.Thirst < Constants.ThirstyThreshold && hasDrinkInInventory)
            {
                var idx = inventory.items.ToList().FindIndex(x => x.Type == ItemType.Drink);
                if (idx > -1)
                {
                    inventory.items[idx] = new Item { Type = ItemType.None, Id = -1 };
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
                    EventBus.Send(new LogEvent { Message = $"{identity.FirstName} {identity.LastName} is starting to be very Thirsty"});
                    living.Thirsty = true;
                    break;
                    
                case { Thirst: <= 0, Thirsty: true, Stamina: > 0 }:
                    living.Stamina -= 5;
                    EventBus.Send(new LogEvent { Message = $"{identity.FirstName} {identity.LastName} is Dehydrated"});
                    break;
                    
                case { Thirsty: true, Stamina: <= 0 }:
                    EventBus.Send(new LogEvent { 
                        Message = $"{identity.FirstName} {identity.LastName} is slowly dying from Dehydration" 
                    });
                    living.Damage += 1;
                    psychology.EmotionalBalance -= 20;
                    psychology.Stress += 20;
                    break;
            }

            if (living.Thirst < Constants.ThirstyThreshold && !hasDrinkInInventory && !objectives.CurrentObjectives.IsHighestPriority(typeof(BuyItem))) {
                objectives.CurrentObjectives.SetNewHighestPriority(new BuyItem { Priority = 10, ItemId = 2 });
                EventBus.Send(new LogEvent { 
                        Message = $"{identity.FirstName} {identity.LastName} has set a new high priority objective: buy drink" 
                });
            }
        });
    }
}
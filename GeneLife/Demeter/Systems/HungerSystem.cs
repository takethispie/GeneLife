﻿using Arch.Bus;
using Arch.Core;
using Arch.System;
using GeneLife.Athena.Components;
using GeneLife.Athena.Core.Objectives;
using GeneLife.Athena.Extensions;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Characters;
using GeneLife.Core.Data;
using GeneLife.Core.Entities.Factories;
using GeneLife.Core.Events;
using GeneLife.Core.Extensions;
using GeneLife.Core.Items;

namespace GeneLife.Demeter.Systems;

internal sealed class HungerSystem : BaseSystem<World, float>
{
    private readonly QueryDescription livingEntities = new();
    private float _tickAccumulator;

    public HungerSystem(World world, ArchetypeFactory archetypeFactory) : base(world)
    {
        _tickAccumulator = 0;
        livingEntities.All = archetypeFactory.Build("person")
            .Append(typeof(Objectives))
            .ToArray();
    }

    public override void Update(in float delta)
    {
        _tickAccumulator += delta;
        //TODO move accumulator to accumulators component or living component
        if (_tickAccumulator >= Constants.TicksPerDay)
        {
            _tickAccumulator = 0;
            World.ParallelQuery(
                in livingEntities,
                (ref Living living, ref Identity identity, ref Psychology psychology, ref Objectives objectives, ref Inventory inventory) =>
            {
                living.Hunger -= 1;

                if (living.Hunger < Constants.HungryThreshold && inventory.items.Any(x => x.Type == ItemType.Food))
                {
                    var idx = inventory.items.ToList().FindIndex(x => x.Type == ItemType.Food);
                    if (idx > -1)
                    {
                        inventory.items[idx] = new Item { Type = ItemType.None, Id = -1 };
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
                        EventBus.Send(new LogEvent { 
                            Message = $"{identity.FirstName} {identity.LastName} is slowly dying from starvation" 
                        });
                        living.Damage += 1;
                        psychology.EmotionalBalance -= 20;
                        psychology.Stress += 20;
                        break;
                }

                if (living.Hunger < Constants.HungryThreshold 
                        && !inventory.items.Any(x => x.Type == ItemType.Food)
                        && !objectives.CurrentObjectives.IsHighestPriority(typeof(BuyItem))) {
                    objectives.CurrentObjectives.SetNewHighestPriority(new BuyItem { Priority = 10, ItemId = 1 });
                    EventBus.Send(new LogEvent { 
                            Message = $"{identity.FirstName} {identity.LastName} has set a new high priority objective: buy food" 
                    });
                }
            });
        }
    }
}
﻿using Arch.Bus;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.Core.Utils;
using Arch.System;
using GeneLife.Athena.Components;
using GeneLife.Athena.Core.Objectives;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Characters;
using GeneLife.Core.Data;
using GeneLife.Core.Entities.Factories;
using GeneLife.Core.Events;
using GeneLife.Core.Extensions;
using GeneLife.Core.Items;

namespace GeneLife.Demeter.Systems;

internal sealed class EatingSystem : BaseSystem<World, float>
{
    private readonly QueryDescription livingEntitiesWithObjective = new();
    private readonly QueryDescription livingEntitiesWithoutObjectives = new();
    private float _tickAccumulator;
    
    public EatingSystem(World world, ArchetypeFactory archetypeFactory) : base(world)
    {
        _tickAccumulator = 0;
        livingEntitiesWithObjective.All = archetypeFactory.Build("person").Append(typeof(Objectives)).ToArray();
        livingEntitiesWithoutObjectives.All = archetypeFactory.Build("person");
        livingEntitiesWithoutObjectives.None = new ComponentType[] { typeof(Objectives) };
    }

    public override void Update(in float delta)
    {
        _tickAccumulator += delta;
        if (_tickAccumulator >= 10)
        {
            _tickAccumulator = 0;
            World.ParallelQuery(in livingEntitiesWithObjective,
                (ref Living living, ref Identity identity, ref Inventory inventory, ref Objectives objectives) =>
                {
                    if (living.Hungry && !objectives.CurrentObjectives.Any(x => x is Eat))
                    {
                        objectives.CurrentObjectives = objectives.CurrentObjectives.Append(new Eat(10)).ToArray();
                        EventBus.Send(new LogEvent { Message = $"{identity.FullName()} has set a new high priority objective: eating"});
                    }
                    
                    if (objectives.CurrentObjectives.Any(x => x is Eat) && inventory.items.Any(x => x.Type == ItemType.Food))
                    {
                        var idx = inventory.items.ToList().FindIndex(x => x.Type == ItemType.Food);
                        if (idx <= -1) return;
                        inventory.items[idx] = new Item { Type = ItemType.None, Id = -1 };
                        living.Hungry = false;
                        living.Hunger = Constants.MaxHunger;
                        objectives.CurrentObjectives = objectives.CurrentObjectives.Where(x => x is not Eat).ToArray();
                        EventBus.Send(new LogEvent
                            { Message = $"{identity.FirstName} {identity.LastName} has filled his/her stomach" });
                    }
                });
            
            World.ParallelQuery(in livingEntitiesWithoutObjectives,
                (in Entity entity) =>
                {
                    var identity = entity.Get<Identity>();
                    if (!entity.Get<Living>().Hungry) return;
                    entity.Add(new Objectives
                    {
                        CurrentObjectives = new []
                        {
                            new Eat(10)
                        }
                    });
                    EventBus.Send(new LogEvent { Message = $"{identity.FullName()} has set a new high priority objective: eating"});
                });
        }
    }
}
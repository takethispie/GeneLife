using Arch.Bus;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.Core.Utils;
using Arch.System;
using GeneLife.Athena.Components;
using GeneLife.Athena.Core.Objectives;
using GeneLife.Core.Components.Characters;
using GeneLife.Core.Data;
using GeneLife.Core.Entities.Factories;
using GeneLife.Core.Events;
using GeneLife.Core.Extensions;
using GeneLife.Core.Items;

namespace GeneLife.Demeter.Systems;

internal sealed class DrinkingSystem : BaseSystem<World, float>
{
    private readonly QueryDescription livingEntitiesWithObjective = new();
    private readonly QueryDescription livingEntitiesWithoutObjectives = new();
    private float _tickAccumulator;
    
    public DrinkingSystem(World world, ArchetypeFactory archetypeFactory) : base(world)
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
                    if (living.Thirsty && !objectives.CurrentObjectives.Any(x => x is Drink))
                    {
                        objectives.CurrentObjectives = objectives.CurrentObjectives.Append(new Drink(10)).ToArray();
                        EventBus.Send(new LogEvent { Message = $"{identity.FullName()} has set a new high priority objective: drinking"});
                    }
                    
                    if (objectives.CurrentObjectives.Any(x => x is Drink) && inventory.items.Any(x => x.Type == ItemType.Drink))
                    {
                        var idx = inventory.items.ToList().FindIndex(x => x.Type == ItemType.Drink);
                        if (idx <= -1) return;
                        inventory.items[idx] = new Item { Type = ItemType.None, Id = -1 };
                        living.Thirsty = false;
                        living.Thirst = Constants.MaxThirst;
                        objectives.CurrentObjectives = objectives.CurrentObjectives.Where(x => x is not Drink).ToArray();
                        EventBus.Send(new LogEvent
                            { Message = $"{identity.FirstName} {identity.LastName} is totally hydrated" });
                    }
                });
            
            World.ParallelQuery(in livingEntitiesWithoutObjectives,
                (in Entity entity) =>
                {
                    var identity = entity.Get<Identity>();
                    if (!entity.Get<Living>().Thirsty) return;
                    entity.Add(new Objectives
                    {
                        CurrentObjectives = new IObjective[]
                        {
                            new Drink(10)
                        }
                    });
                    EventBus.Send(new LogEvent { Message = $"{identity.FullName()} has set a new high priority objective: drinking"});
                });
        }
    }
}
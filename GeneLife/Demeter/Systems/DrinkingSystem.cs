using Arch.Bus;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.System;
using GeneLife.Common.Components;
using GeneLife.Common.Components.Utils;
using GeneLife.Common.Data;
using GeneLife.Core.Events;
using GeneLife.Core.Items;
using GeneLife.Core.Objectives;
using GeneLife.Utils;

namespace GeneLife.Demeter.Systems;

public class DrinkingSystem : BaseSystem<World, float>
{
    private readonly QueryDescription livingEntitiesWithObjective = new QueryDescription().WithAll<Living, Identity, Inventory, Objectives>();
    private readonly QueryDescription livingEntitiesWithoutObjectives = new QueryDescription().WithAll<Living, Identity, Inventory>().WithNone<Objectives>();
    private float _tickAccumulator;
    
    public DrinkingSystem(World world) : base(world)
    {
        _tickAccumulator = 0;
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
                        living.Thirst = 20;
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
                        CurrentObjectives = new []
                        {
                            new Drink(10)
                        }
                    });
                    EventBus.Send(new LogEvent { Message = $"{identity.FullName()} has set a new high priority objective: drinking"});
                });
        }
    }
}
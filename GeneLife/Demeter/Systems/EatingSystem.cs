using Arch.Bus;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.System;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Characters;
using GeneLife.Core.Events;
using GeneLife.Core.Items;
using GeneLife.Core.Objectives;
using GeneLife.Core.Utils;

namespace GeneLife.Demeter.Systems;

public class EatingSystem : BaseSystem<World, float>
{
    private readonly QueryDescription livingEntitiesWithObjective = new QueryDescription().WithAll<Living, Identity, Inventory, Objectives>();
    private readonly QueryDescription livingEntitiesWithoutObjectives = new QueryDescription().WithAll<Living, Identity, Inventory>().WithNone<Objectives>();
    private float _tickAccumulator;
    
    public EatingSystem(World world) : base(world)
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
                        living.Hunger = 10;
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
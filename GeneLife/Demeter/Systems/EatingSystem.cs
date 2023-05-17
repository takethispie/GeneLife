using Arch.Bus;
using Arch.Core;
using Arch.System;
using GeneLife.Common.Components;
using GeneLife.Common.Components.Utils;
using GeneLife.Common.Data;
using GeneLife.Core.Items;
using GeneLife.Core.Objectives;

namespace GeneLife.Demeter.Systems;

public class EatingSystem : BaseSystem<World, float>
{
    private readonly QueryDescription livingEntities = new QueryDescription().WithAll<Living, Identity, Inventory, Objectives>();
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
            World.ParallelQuery(in livingEntities,
                (ref Living living, ref Identity identity, ref Inventory inventory, ref Objectives objectives) =>
                {
                    if (!objectives.CurrentObjectives.Any(x => x is Eat) || inventory.items.All(x => x.Type != ItemType.Food)) return;
                    var idx = inventory.items.ToList().FindIndex(x => x.Type == ItemType.Food);
                    if (idx <= -1) return;
                    inventory.items[idx] = new Item { Type = ItemType.None, Id = -1 };
                    living.Hungry = false;
                    living.Hunger = 10;
                    EventBus.Send(new LogEvent { Message = $"{identity.FirstName} {identity.LastName} has filled his/her stomach"});
                });
        }
    }
}
using Arch.Bus;
using Arch.Core;
using Arch.System;
using GeneLife.Common.Components;
using GeneLife.Common.Components.Utils;
using GeneLife.Common.Data;

namespace GeneLife.Demeter.Systems;

public class HungerSystem : BaseSystem<World, float>
{
    private readonly QueryDescription livingEntities = new QueryDescription().WithAll<Living, Identity, Psychology>();
    private float _tickAccumulator;
    
    public HungerSystem(World world) : base(world)
    {
        _tickAccumulator = 0;
    }

    public override void Update(in float delta)
    {
        _tickAccumulator += delta;
        if (_tickAccumulator >= 300)
        {
            _tickAccumulator = 0;
            World.Query(in livingEntities, (ref Living living, ref Identity identity, ref Psychology psychology) =>
            {
                living.Hunger -= 2;
                if (living.Hunger <= 2)
                {
                    EventBus.Send(new LogEvent { Message = $"{identity.FirstName} {identity.LastName} is starting to be very hungry"});
                    living.Hungry = true;
                }
                else if(living.Hunger <= 0)
                {
                    living.Stamina -= 5;
                    EventBus.Send(new LogEvent { Message = $"{identity.FirstName} {identity.LastName} is starving"});
                }

                if (living is not { Hungry: true, Stamina: <= 0 }) return;
                living.Damage += 1;
                psychology.EmotionalBalance -= 20;
                psychology.Stress += 20;
            });
        }
    }
}
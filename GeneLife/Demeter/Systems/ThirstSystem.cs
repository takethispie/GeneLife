using Arch.Bus;
using Arch.Core;
using Arch.System;
using GeneLife.Common.Components;
using GeneLife.Common.Components.Utils;
using GeneLife.Common.Data;
using GeneLife.Core.Events;

namespace GeneLife.Demeter.Systems;

public class ThirstSystem : BaseSystem<World, float>
{
    private readonly QueryDescription livingEntities = new QueryDescription().WithAll<Living, Identity, Psychology>();
    private float _tickAccumulator;
    
    public ThirstSystem(World world) : base(world)
    {
        _tickAccumulator = 0;
    }

    public override void Update(in float delta)
    {
        _tickAccumulator += delta;
        //TODO move accumulator to accumulators component or living component
        if (_tickAccumulator >= Constants.TickPerDay)
        {
            _tickAccumulator = 0;
            World.ParallelQuery(in livingEntities, (ref Living living, ref Identity identity, ref Psychology psychology) =>
            {
                living.Thirst -= 4;
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
                        EventBus.Send(new LogEvent
                            { Message = $"{identity.FirstName} {identity.LastName} is slowly dying from Dehydration" });
                        living.Damage += 1;
                        psychology.EmotionalBalance -= 20;
                        psychology.Stress += 20;
                        break;
                }
            });
        }
    }
}
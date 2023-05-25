using Arch.Bus;
using Arch.Core;
using Arch.System;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Characters;
using GeneLife.Core.Data;
using GeneLife.Core.Events;

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
        //TODO move accumulator to accumulators component or living component
        if (_tickAccumulator >= Constants.TickPerDay)
        {
            _tickAccumulator = 0;
            World.Query(in livingEntities, (ref Living living, ref Identity identity, ref Psychology psychology) =>
            {
                living.Hunger -= 1;
                switch (living)
                {
                    case { Hungry: false } when living.Hunger <= Constants.HungryThreshold:
                        EventBus.Send(new LogEvent { Message = $"{identity.FirstName} {identity.LastName} is starting to be very hungry"});
                        living.Hungry = true;
                        break;
                    
                    case { Hunger: <= 0, Hungry: true, Stamina: > 0 }:
                        living.Stamina -= 5;
                        EventBus.Send(new LogEvent { Message = $"{identity.FirstName} {identity.LastName} is starving"});
                        break;
                    
                    case { Hungry: true, Stamina: <= 0 }:
                        EventBus.Send(new LogEvent
                            { Message = $"{identity.FirstName} {identity.LastName} is slowly dying from starvation" });
                        living.Damage += 1;
                        psychology.EmotionalBalance -= 20;
                        psychology.Stress += 20;
                        break;
                }
            });
        }
    }
}
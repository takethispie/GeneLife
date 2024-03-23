using Arch.Core;
using Arch.System;
using GeneLife.Core.Data;

namespace GeneLife.Core.Systems;

public sealed class GlobalSystem : BaseSystem<World, float>
{

    public GlobalSystem(World world) : base(world)
    {
    }

    public override void Update(in float delta)
    {
        Clock.Tick = Constants.TicksPerDay switch
        {
            24 when Clock.Tick is 1 => AddHour(),
            48 when Clock.Tick is 2 => AddHour(),
            192 when Clock.Tick is 8 => AddHour(),
            240 when Clock.Tick is 10 => AddHour(),
            _ => Clock.Tick + 1
        };

    }

    public static int AddHour()
    {
        Clock.Time = Clock.Time.AddHours(1);
        return 0;
    }
}
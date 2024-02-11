using Arch.Bus;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.Core.Utils;
using Arch.System;
using GeneLife.Core.Components;
using GeneLife.Core.Entities.Factories;
using GeneLife.Core.Events;
using GeneLife.Core.ObjectiveActions;
using GeneLife.Survival.Components;

namespace GeneLife.Core.Systems
{
    internal sealed class GlobalSystem : BaseSystem<World, float>
    {
        private readonly QueryDescription globalEntities = new();
        private int tickPerDay = 10;

        public GlobalSystem(World world) : base(world)
        {
            globalEntities.All = [typeof(Clock)];
        }

        public override void Update(in float delta)
        {
            var globals = new List<Entity>();
            World.GetEntities(globalEntities, globals);
            if (globals.Count == 0) return;
            var cl = globals.FirstOrDefault().Get<Clock>();
            cl.ticks += delta;
        }
    }
}
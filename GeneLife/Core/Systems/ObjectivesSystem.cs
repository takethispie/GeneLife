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
    internal sealed class ObjectivesSystem : BaseSystem<World, float>
    {
        private readonly QueryDescription entitiesWithObjectives = new();

        public ObjectivesSystem(World world, ArchetypeFactory archetypeFactory) : base(world)
        {
            entitiesWithObjectives.All = [typeof(Objectives), typeof(Living), typeof(Position), typeof(Home)];
        }

        public override void Update(in float delta)
        {
            World.Query(in entitiesWithObjectives, (ref Objectives obj, ref Position position, ref Home home) =>
            {
                if (obj.CurrentObjectives.All(x => x is EmptyObjective))
                {
                    obj.TickWithNoObjectives += 1;
                    if (obj.TickWithNoObjectives > 5 && position.Coordinates != home.Position)
                    {
                        obj.CurrentObjectives = obj.CurrentObjectives.Prepend(new MoveTo(10, home.Position)).ToArray();
                        obj.TickWithNoObjectives = 0;
                    }
                }
                else obj.TickWithNoObjectives = 0;
            });

        }
    }
}
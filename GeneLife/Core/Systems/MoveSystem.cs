using Arch.Core;
using Arch.Core.Extensions;
using Arch.System;
using GeneLife.Core.Components;
using GeneLife.Core.Entities.Factories;
using GeneLife.Athena.Core.Objectives;
using GeneLife.Core.Extensions;
using Arch.Bus;
using GeneLife.Core.Events;
using System.Numerics;

namespace GeneLife.Core.Systems
{
    internal class MoveSystem : BaseSystem<World, float>
    {

        private readonly QueryDescription movingEntities = new QueryDescription().WithAll<Moving, Position, Objectives>();
        private readonly QueryDescription needToMoveEntitie = new();

        public MoveSystem(World world, ArchetypeFactory archetypeFactory) : base(world)
        {
            needToMoveEntitie.All = archetypeFactory.Build("person");
        }

        public override void Update(in float t)
        {
            var movable = new List<Entity>();
            World.GetEntities(in needToMoveEntitie, movable);
            _ = movable
                .Where(x => x.Has<Objectives>())
                .Where(x =>
                {
                    var objectives = x.Get<Objectives>();
                    var isMoving = x.Has<Moving>();
                    if (objectives.IsHighestPriority(typeof(MoveTo)) && !isMoving)
                    {
                        var moveTo = objectives.CurrentObjectives.Where(x => x is MoveTo).FirstOrDefault();
                        if (moveTo == null) return false;
                        x.Add(new Moving { Velocity = 100f, Target = ((MoveTo)moveTo).Target });
                        EventBus.Send(new LogEvent
                        {
                            Message = $"entity {x.Id} started to move toward {((MoveTo)moveTo).Target}"
                        });
                    }
                    else if (!objectives.IsHighestPriority(typeof(MoveTo)) && isMoving)
                        //TODO might need to do position checks 
                        x.Remove<Moving>();
                    return false;
                }).ToList();
            World.Query(in movingEntities, (ref Moving moving, ref Position position, ref Objectives objectives) =>
            {
                if (position.Coordinates == moving.Target)
                    objectives.RemoveHighestPriority();
                else position.Coordinates = position.Coordinates.MovePointTowards(moving.Target, moving.Velocity);
            });
        }
    }
}
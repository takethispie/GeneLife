using Arch.Core;
using Arch.Core.Extensions;
using Arch.System;
using GeneLife.Athena.Components;
using GeneLife.Core.Components;
using GeneLife.Core.Entities.Factories;
using GeneLife.Athena.Extensions;
using GeneLife.Athena.Core.Objectives;
using GeneLife.Core.Extensions;
using Arch.Bus;
using GeneLife.Core.Events;

namespace GeneLife.Athena.Systems;
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
                if(objectives.CurrentObjectives.IsHighestPriority(typeof(MoveTo)) && !x.Has<Moving>())
                {
                    var moveTo = objectives.CurrentObjectives.Where(x => x.Name == MoveTo.GetName()).FirstOrDefault();
                    if (moveTo == null) return false;
                    x.Add(new Moving { Velocity = 100f, Target = ((MoveTo)moveTo).Target });
                    EventBus.Send(new LogEvent
                    {
                        Message = $"entity {x.Id} started to move toward {((MoveTo)moveTo).Target}"
                    });
                }
                return false;
            }).ToList();
        World.Query(in movingEntities, (ref Moving moving, ref Position position, ref Objectives objectives) => {
            if(position.Coordinates == moving.Target)
            {
                objectives.CurrentObjectives = objectives.CurrentObjectives.RemoveHighestPriority().ToArray();
            } 
            else position.Coordinates = position.Coordinates.MovePointTowards(moving.Target, moving.Velocity);
        });
    }
}

using Arch.Core;
using Arch.Core.Extensions;
using Arch.System;
using GeneLife.Core.Components;
using GeneLife.Core.Entities.Factories;
using GeneLife.Core.Extensions;
using Arch.Bus;
using GeneLife.Core.Events;
using GeneLife.Core.Planning;
using GeneLife.Core.Data;
using GeneLife.Core.Planning.Objective;

namespace GeneLife.Core.Systems;

internal class MoveSystem : BaseSystem<World, float>
{

    private readonly QueryDescription movingEntities = new QueryDescription().WithAll<Moving, Position, Planner>();
    private readonly QueryDescription needToMoveEntitie = new();

    public MoveSystem(World world, ArchetypeFactory archetypeFactory) : base(world)
    {
        needToMoveEntitie.All = archetypeFactory.Build("person");
    }

    private static bool SetMoving(Entity entity, MoveTo moveTo)
    {
        entity.Add(new Moving { Velocity = Constants.WalkingSpeed, Target = moveTo.Target });
        EventBus.Send(new LogEvent
        {
            Message = $"entity {entity.Id} started to move toward {moveTo.Target}"
        });
        return false;
    }

    public override void Update(in float t)
    {
        var movable = new List<Entity>();
        World.GetEntities(in needToMoveEntitie, movable);
        _ = movable
            .Where(x => x.Has<Planner>())
            .Where(x =>
            {
                return x.Get<Planner>().GetSlot(Clock.Time) switch
                {
                    MoveTo slot when !x.Has<Moving>() => SetMoving(x, slot),
                    _ => false
                };
            }).ToList();
        World.Query(in movingEntities, (ref Moving moving, ref Position position, ref Planner objectives) =>
        {
            if (position.Coordinates != moving.Target)
                position.Coordinates = position.Coordinates.MovePointTowards(moving.Target, moving.Velocity);
        });
    }
}
using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Gen;
using Genelife.Components.Map;
using Genelife.Map;
using Genelife.Application.Systems;

namespace Genelife.Application.Systems;

public sealed class MovementSystem : ISimulationSystem
{
    private readonly World world;
    private readonly WorldMap map;
    private readonly MovementIndex index;
    private readonly QueryDescription query;
    private readonly float defaultSpeed;

    public MovementSystem(World world, WorldMap map, MovementIndex index, float defaultSpeed = 4f)
    {
        this.world = world;
        this.map = map;
        this.index = index;
        this.defaultSpeed = defaultSpeed;
        query = new QueryDescription().WithAll<TilePosition, MoveTarget, MovementState>();
    }

    public string Name => "Movement";
    public int Order => 40;
    public Cadence Cadence => Cadence.EveryTick;

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.DeltaTime);

    public void Update(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        world.Query(in query, (Entity entity, ref TilePosition position, ref MoveTarget target, ref MovementState movement) =>
        {
            if (!target.HasTarget)
            {
                movement.IsMoving = false;
                movement.Progress = 0f;
                return;
            }

            if (movement.SpeedTilesPerSecond <= 0f)
                movement.SpeedTilesPerSecond = defaultSpeed;

            if (!movement.IsMoving)
            {
                if (index.TryTakeNext(entity, out var next))
                {
                    if (!map.IsWalkable(next) || !map.IsWalkable(position.Tile))
                    {
                        Cancel(ref target, ref movement);
                        index.Clear(entity);
                        return;
                    }

                    movement.From = position.Tile;
                    movement.To = next;
                    movement.Progress = 0f;
                    movement.IsMoving = true;
                }
                else if (!index.HasPending(entity))
                {
                    Cancel(ref target, ref movement);
                    index.Clear(entity);
                    return;
                }
            }

            if (movement.IsMoving)
            {
                movement.Progress += deltaTime * movement.SpeedTilesPerSecond;

                if (movement.Progress >= 1f)
                {
                    position.Tile = movement.To;
                    movement.Progress = 0f;
                    movement.IsMoving = false;
                    movement.From = movement.To;
                }

                UpdateLocation(entity, position, movement);
            }
            else
            {
                UpdateLocation(entity, position, movement);
            }

            if (!index.HasRoute(entity) && !movement.IsMoving)
            {
                target.HasTarget = false;
                index.Clear(entity);
            }
        });
    }

    private void UpdateLocation(Entity entity, TilePosition position, MovementState movement)
    {
        if (!world.Has<Location>(entity))
            return;

        float x = position.Tile.X;
        float y = position.Tile.Y;
        if (movement.IsMoving)
        {
            x += (movement.To.X - movement.From.X) * movement.Progress;
            y += (movement.To.Y - movement.From.Y) * movement.Progress;
        }

        var (worldX, worldY) = map.Options.ToWorld(new TileCoord(0, 0), x, y);
        world.Get<Location>(entity) = new Location(worldX, worldY);
    }

    private static void Cancel(ref MoveTarget target, ref MovementState movement)
    {
        target.HasTarget = false;
        movement.IsMoving = false;
        movement.Progress = 0f;
    }
}

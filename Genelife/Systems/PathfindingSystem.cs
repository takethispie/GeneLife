using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Map;
using Genelife.Map;
using Genelife.Systems;

namespace Genelife.Systems;

public sealed class PathfindingSystem : ISimulationSystem
{
    private readonly World world;
    private readonly WorldMap map;
    private readonly MovementIndex index;
    private readonly int budget;

    public PathfindingSystem(World world, WorldMap map, MovementIndex index, int budget = 32)
    {
        this.world = world;
        this.map = map;
        this.index = index;
        this.budget = Math.Max(1, budget);
    }

    public string Name => "Pathfinding";
    public int Order => 35;
    public Cadence Cadence => Cadence.EveryTick;

    void ISimulationSystem.Update(in SimulationContext context) => Update();

    public void Update()
    {
        foreach (var request in index.TakePending(budget))
        {
            if (!world.IsAlive(request.Entity))
                continue;

            var path = TilePathfinder.FindPath(map, request.Start, request.Target, budget * 256);
            if (path is null)
            {
                if (world.Has<MoveTarget>(request.Entity))
                {
                    var target = world.Get<MoveTarget>(request.Entity);
                    target.HasTarget = false;
                    world.Get<MoveTarget>(request.Entity) = target;
                }
                index.Clear(request.Entity);
                continue;
            }

            index.SetRoute(request.Entity, path);
            if (world.Has<MoveTarget>(request.Entity))
            {
                var target = world.Get<MoveTarget>(request.Entity);
                target.PathVersion = index.PathVersion;
                world.Get<MoveTarget>(request.Entity) = target;
            }
        }
    }
}

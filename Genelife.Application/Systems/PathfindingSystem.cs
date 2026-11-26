using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Map;
using Genelife.Map;
using Genelife.Application.Systems;

namespace Genelife.Application.Systems;

public sealed class PathfindingSystem : ISimulationSystem
{
    private readonly World world;
    private readonly WorldMap map;
    private readonly MovementIndex index;
    private readonly int budget;
    private readonly int maxIterationsPerTick;

    public PathfindingSystem(World world, WorldMap map, MovementIndex index, int budget = 32, int maxIterationsPerTick = 16_384)
    {
        this.world = world;
        this.map = map;
        this.index = index;
        this.budget = Math.Max(1, budget);
        this.maxIterationsPerTick = Math.Max(this.budget, maxIterationsPerTick);
    }

    public string Name => "Pathfinding";
    public int Order => 35;
    public Cadence Cadence => Cadence.EveryTick;

    void ISimulationSystem.Update(in SimulationContext context) => Update();

    public void Update()
    {
        var remainingIterations = maxIterationsPerTick;

        foreach (var request in index.TakePending(budget))
        {
            if (remainingIterations <= 0)
                break;

            if (!world.IsAlive(request.Entity))
                continue;

            var limit = Math.Min(budget * 256, remainingIterations);
            var path = TilePathfinder.FindPath(map, request.Start, request.Target, limit, out var usedIterations);
            remainingIterations -= usedIterations;
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

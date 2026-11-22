using Arch.Core;
using Genelife.Components.Map;
using Genelife.Map;

namespace Genelife.Map;

public readonly record struct PathRequest(
    Entity Entity,
    TileCoord Start,
    TileCoord Target,
    int MapVersion,
    int RequestVersion);

public sealed class MovementIndex
{
    private readonly Dictionary<Entity, Queue<TileCoord>> routes = new();
    private readonly Dictionary<Entity, PathRequest> pending = new();
    private int pathVersion = 1;

    public int PendingCount => pending.Count;
    public int PathVersion => pathVersion;

    public void RequestPath(Entity entity, TileCoord start, TileCoord target, int mapVersion)
    {
        routes.Remove(entity);
        pending[entity] = new PathRequest(entity, start, target, mapVersion, pathVersion);
    }

    public bool HasPending(Entity entity) => pending.ContainsKey(entity);

    public IReadOnlyList<PathRequest> TakePending(int max)
    {
        if (max <= 0 || pending.Count == 0)
            return Array.Empty<PathRequest>();

        var result = pending.Values.Take(max).ToList();
        foreach (var request in result)
            pending.Remove(request.Entity);
        return result;
    }

    public void SetRoute(Entity entity, IReadOnlyList<TileCoord> route)
    {
        routes[entity] = new Queue<TileCoord>(route);
        pathVersion++;
    }

    public bool TryTakeNext(Entity entity, out TileCoord next)
    {
        next = default;
        if (!routes.TryGetValue(entity, out var route) || route.Count == 0)
            return false;

        next = route.Dequeue();
        return true;
    }

    public bool HasRoute(Entity entity) =>
        routes.TryGetValue(entity, out var route) && route.Count > 0;

    public void Clear(Entity entity)
    {
        routes.Remove(entity);
        pending.Remove(entity);
    }

    public void RebuildRequests(World world, int mapVersion)
    {
        routes.Clear();
        pending.Clear();

        var query = new QueryDescription().WithAll<TilePosition, MoveTarget>();
        world.Query(in query, (Entity entity, ref TilePosition position, ref MoveTarget target) =>
        {
            if (target.HasTarget)
                RequestPath(entity, position.Tile, target.Tile, mapVersion);
        });
    }
}

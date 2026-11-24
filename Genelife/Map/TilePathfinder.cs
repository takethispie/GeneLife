namespace Genelife.Map;

public static class TilePathfinder
{
    private static readonly (int X, int Y)[] Directions =
    {
        (0, -1),
        (0, 1),
        (-1, 0),
        (1, 0)
    };

    public static List<TileCoord>? FindPath(
        WorldMap map,
        TileCoord start,
        TileCoord target,
        int maxLength = 4096) =>
        FindPath(map, start, target, maxLength, out _);

    /// <summary>
    /// Finds a path and reports how many nodes were expanded, so callers can
    /// enforce a per-tick work budget based on actual cost rather than the
    /// worst-case limit.
    /// </summary>
    public static List<TileCoord>? FindPath(
        WorldMap map,
        TileCoord start,
        TileCoord target,
        int maxLength,
        out int iterations)
    {
        iterations = 0;

        if (!map.IsWalkable(start) || !map.IsWalkable(target))
            return null;
        if (start == target)
            return new List<TileCoord>();

        var open = new PriorityQueue<TileCoord, (int F, int G, int X, int Y)>();
        var cameFrom = new Dictionary<TileCoord, TileCoord>();
        var gScore = new Dictionary<TileCoord, int> { [start] = 0 };
        open.Enqueue(start, (Heuristic(start, target), 0, start.X, start.Y));

        while (open.TryDequeue(out var current, out _))
        {
            iterations++;
            if (current == target)
                return Reconstruct(cameFrom, start, target);

            var currentG = gScore[current];
            if (currentG >= maxLength)
                continue;

            foreach (var (dx, dy) in Directions)
            {
                var next = new TileCoord(current.X + dx, current.Y + dy);
                if (!map.IsWalkable(next))
                    continue;

                var tentative = currentG + 1;
                if (gScore.TryGetValue(next, out var known) && tentative >= known)
                    continue;

                gScore[next] = tentative;
                cameFrom[next] = current;
                var f = tentative + Heuristic(next, target);
                open.Enqueue(next, (f, tentative, next.X, next.Y));
            }
        }

        return null;
    }

    private static int Heuristic(TileCoord from, TileCoord to) => from.ManhattanDistanceTo(to);

    private static List<TileCoord> Reconstruct(
        Dictionary<TileCoord, TileCoord> cameFrom,
        TileCoord start,
        TileCoord target)
    {
        var path = new List<TileCoord>();
        var current = target;
        while (current != start)
        {
            path.Add(current);
            if (!cameFrom.TryGetValue(current, out current))
                return new List<TileCoord>();
        }

        path.Reverse();
        return path;
    }
}

using System.Numerics;
using Arch.Core;

namespace Genelife.Systems;

public readonly record struct WorldBounds(float MinX, float MinY, float MaxX, float MaxY)
{
    public float Width => MaxX - MinX;
    public float Height => MaxY - MinY;

    public WorldBounds(float halfExtent)
        : this(-halfExtent, -halfExtent, halfExtent, halfExtent)
    {
    }

    public bool Contains(Vector2 point) =>
        point.X >= MinX && point.X <= MaxX && point.Y >= MinY && point.Y <= MaxY;

    public Vector2 Clamp(Vector2 point) =>
        new(
            Math.Clamp(point.X, MinX, MaxX),
            Math.Clamp(point.Y, MinY, MaxY));
}

public sealed class SpatialIndex
{
    private readonly float cellSize;
    private readonly WorldBounds bounds;
    private readonly Dictionary<(int cx, int cy), List<Entity>> cells = new();
    private readonly Dictionary<Entity, (int cx, int cy)> entityCells = new();

    public float CellSize => cellSize;
    public WorldBounds Bounds => bounds;
    public int CellCount => cells.Count;
    public int EntityCount => entityCells.Count;

    public SpatialIndex(float cellSize, WorldBounds bounds)
    {
        if (cellSize <= 0f)
            throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be positive.");
        this.cellSize = cellSize;
        this.bounds = bounds;
    }

    public static SpatialIndex CreateDefault() =>
        new(cellSize: 10f, bounds: new WorldBounds(halfExtent: 1000f));

    private (int cx, int cy) Cell(Vector2 position)
    {
        var clamped = bounds.Clamp(position);
        return ((int)MathF.Floor(clamped.X / cellSize),
                (int)MathF.Floor(clamped.Y / cellSize));
    }

    private List<Entity> GetOrCreateCell((int cx, int cy) key)
    {
        if (!cells.TryGetValue(key, out var list))
        {
            list = new List<Entity>(16);
            cells[key] = list;
        }
        return list;
    }

    public void Add(Entity entity, Vector2 position)
    {
        var key = Cell(position);
        GetOrCreateCell(key).Add(entity);
        entityCells[entity] = key;
    }

    public void Move(Entity entity, Vector2 newPosition)
    {
        if (!entityCells.TryGetValue(entity, out var oldKey))
            return;

        var newKey = Cell(newPosition);
        if (oldKey == newKey)
            return;

        if (cells.TryGetValue(oldKey, out var oldList))
        {
            oldList.Remove(entity);
            if (oldList.Count == 0)
                cells.Remove(oldKey);
        }

        GetOrCreateCell(newKey).Add(entity);
        entityCells[entity] = newKey;
    }

    public void Remove(Entity entity)
    {
        if (!entityCells.Remove(entity, out var key))
            return;

        if (cells.TryGetValue(key, out var list))
        {
            list.Remove(entity);
            if (list.Count == 0)
                cells.Remove(key);
        }
    }

    public void Clear()
    {
        cells.Clear();
        entityCells.Clear();
    }

    public IEnumerable<Entity> GetEntitiesInCell(int cx, int cy)
    {
        return cells.TryGetValue((cx, cy), out var list) ? list : [];
    }

    public IEnumerable<Entity> GetEntitiesInAABB(Vector2 min, Vector2 max)
    {
        var minCell = Cell(min);
        var maxCell = Cell(max);

        for (int cx = minCell.cx; cx <= maxCell.cx; cx++)
        for (int cy = minCell.cy; cy <= maxCell.cy; cy++)
            if (cells.TryGetValue((cx, cy), out var list))
                foreach (var entity in list)
                    yield return entity;
    }

    public List<(Entity Entity, float DistanceSq)> GetNeighbors(Vector2 center, float radius, Func<Entity, Vector2> getPosition)
    {
        float radiusSq = radius * radius;
        var minCell = Cell(new Vector2(center.X - radius, center.Y - radius));
        var maxCell = Cell(new Vector2(center.X + radius, center.Y + radius));

        var results = new List<(Entity, float)>();
        for (int cx = minCell.cx; cx <= maxCell.cx; cx++)
        for (int cy = minCell.cy; cy <= maxCell.cy; cy++)
            if (cells.TryGetValue((cx, cy), out var list))
                foreach (var entity in list)
                {
                    float distSq = Vector2.DistanceSquared(center, getPosition(entity));
                    if (distSq <= radiusSq)
                        results.Add((entity, distSq));
                }

        results.Sort((a, b) => a.Item2.CompareTo(b.Item2));
        return results;
    }

    public IEnumerable<(int cx, int cy)> GetOccupiedCellsInRadius(Vector2 center, float radius)
    {
        var minCell = Cell(new Vector2(center.X - radius, center.Y - radius));
        var maxCell = Cell(new Vector2(center.X + radius, center.Y + radius));

        for (int cx = minCell.cx; cx <= maxCell.cx; cx++)
        for (int cy = minCell.cy; cy <= maxCell.cy; cy++)
            if (cells.ContainsKey((cx, cy)))
                yield return (cx, cy);
    }
}
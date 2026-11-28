using System.Collections;

namespace Genelife.Map;

/// <summary>
/// A lazy, read-only view of the tile flags of one map.
///
/// Rule 1 of <c>docs/research/world-scale-migration-32000.md</c> (§4.1) makes
/// generation a pure function of the coordinate, so the world is never held as
/// a dense <c>TileFlags[width * height]</c> array — at 32000×32000 that array
/// is ~1 GB and cannot exist. A grid is therefore normally classified on demand
/// (<see cref="Lazy"/>), and only falls back to wrapping an already-materialised
/// array when terrain arrives from elsewhere (a restored world, whose terrain is
/// persisted as bytes).
///
/// Enumeration is row-major — <c>y</c> outer from <see cref="MapBounds.MinY"/>,
/// <c>x</c> inner from <see cref="MapBounds.MinX"/> — which is the order the
/// historical dense array used, so a <see cref="ToArray"/> round-trips a
/// previously persisted world byte-for-byte.
/// </summary>
public sealed class TileGrid : IReadOnlyList<TileFlags>
{
    private readonly Func<TileCoord, TileFlags>? classify;
    private readonly TileFlags[]? materialised;

    private TileGrid(MapBounds bounds, Func<TileCoord, TileFlags>? classify, TileFlags[]? materialised)
    {
        Bounds = bounds;
        this.classify = classify;
        this.materialised = materialised;
    }

    /// <summary>An empty grid, used where no world has been generated yet.</summary>
    public static readonly TileGrid Empty = new(default, null, []);

    public MapBounds Bounds { get; }

    /// <summary>A grid whose tiles are computed on demand from a pure classifier.</summary>
    public static TileGrid Lazy(MapBounds bounds, Func<TileCoord, TileFlags> classify) =>
        new(bounds, classify, null);

    /// <summary>A grid backed by an already-materialised, row-major array (a restored world).</summary>
    public static TileGrid FromArray(TileFlags[] flags, MapBounds bounds) =>
        new(bounds, null, flags);

    public TileFlags this[TileCoord tile]
    {
        get
        {
            if (!Bounds.Contains(tile))
                return TileFlags.None;
            if (materialised is not null)
            {
                var index = IndexOf(tile);
                return index >= 0 && index < materialised.Length ? materialised[index] : TileFlags.None;
            }
            return classify!(tile);
        }
    }

    public TileFlags this[int x, int y] => this[new TileCoord(x, y)];

    public int Count => materialised?.Length ?? Bounds.Area;

    public bool IsEmpty => Count == 0;

    public TileFlags this[int index]
    {
        get
        {
            if (materialised is not null)
                return materialised[index];

            var x = Bounds.MinX + index % Bounds.Width;
            var y = Bounds.MinY + index / Bounds.Width;
            return classify!(new TileCoord(x, y));
        }
    }

    /// <summary>Materialises the whole grid, row-major. O(area) — callers that hold a world-sized copy are the reason Phase 2 exists.</summary>
    public TileFlags[] ToArray()
    {
        if (materialised is not null)
            return (TileFlags[])materialised.Clone();

        var array = new TileFlags[Count];
        for (var i = 0; i < array.Length; i++)
            array[i] = this[i];
        return array;
    }

    private int IndexOf(TileCoord tile) =>
        (tile.Y - Bounds.MinY) * Bounds.Width + (tile.X - Bounds.MinX);

    public IEnumerator<TileFlags> GetEnumerator()
    {
        var count = Count;
        for (var i = 0; i < count; i++)
            yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

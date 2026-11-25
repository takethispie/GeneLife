namespace Genelife.Map;

/// <summary>
/// Inclusive tile bounds for the world map.
/// </summary>
public readonly record struct MapBounds(int MinX, int MinY, int MaxX, int MaxY)
{
    public int Width => MaxX - MinX + 1;
    public int Height => MaxY - MinY + 1;
    public int Area => Width * Height;

    public bool Contains(TileCoord tile) =>
        tile.X >= MinX && tile.X <= MaxX &&
        tile.Y >= MinY && tile.Y <= MaxY;

    public TileCoord Clamp(TileCoord tile) =>
        new(Math.Clamp(tile.X, MinX, MaxX), Math.Clamp(tile.Y, MinY, MaxY));

    public bool Contains(MapBounds other) =>
        other.MinX >= MinX && other.MinY >= MinY &&
        other.MaxX <= MaxX && other.MaxY <= MaxY;

    public bool Intersects(MapBounds other) =>
        other.MinX <= MaxX && other.MaxX >= MinX &&
        other.MinY <= MaxY && other.MaxY >= MinY;

    public static MapBounds FromAnchor(TileCoord anchor, Footprint footprint) =>
        new(anchor.X, anchor.Y, anchor.X + footprint.Width - 1, anchor.Y + footprint.Height - 1);

    public override string ToString() => $"[{MinX},{MinY}..{MaxX},{MaxY}]";
}

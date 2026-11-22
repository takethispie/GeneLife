namespace Genelife.Map;

/// <summary>
/// Canonical integer tile position. All placement, collision and movement
/// decisions use tile coordinates; float positions are presentation only.
/// </summary>
public readonly record struct TileCoord(int X, int Y)
{
    public static readonly TileCoord Zero = new(0, 0);

    public int ManhattanDistanceTo(TileCoord other) =>
        Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

    public int ChebyshevDistanceTo(TileCoord other) =>
        Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));

    public TileCoord Offset(int dx, int dy) => new(X + dx, Y + dy);

    public override string ToString() => $"({X},{Y})";
}

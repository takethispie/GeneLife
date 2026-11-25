using Genelife.Map;

namespace Genelife.Api.Snapshots;

public readonly record struct Viewport(int CenterX, int CenterY, int Radius)
{
    public int ExitMargin { get; init; } = 2;

    public bool Contains(TileCoord tile) => Contains(tile.X, tile.Y);

    public bool Contains(int x, int y) =>
        Math.Max(Math.Abs(x - CenterX), Math.Abs(y - CenterY)) <= Radius;

    public bool ContainsWithMargin(int x, int y) =>
        Math.Max(Math.Abs(x - CenterX), Math.Abs(y - CenterY)) <= Radius + ExitMargin;

    public string Key => $"{CenterX}:{CenterY}:{Radius}:{ExitMargin}";
}

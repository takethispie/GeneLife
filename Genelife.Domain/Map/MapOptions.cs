namespace Genelife.Map;

/// <summary>
/// Static configuration for one world map. The map is centred on the origin so
/// the existing float world-space convention and snapshot coordinates remain
/// compatible.
/// </summary>
public sealed record MapOptions
{
    public static readonly MapOptions Default = new();

    public string MapId { get; init; } = "world";
    public int Version { get; init; } = 1;
    public int Seed { get; init; } = 1;
    public int Width { get; init; } = 256;
    public int Height { get; init; } = 256;
    public float TileSize { get; init; } = 1f;

    public MapBounds Bounds => new(
        -(Width / 2),
        -(Height / 2),
        Width - 1 - (Width / 2),
        Height - 1 - (Height / 2));

    public TileCoord ToTile(float x, float y) =>
        new((int)MathF.Round(x / TileSize), (int)MathF.Round(y / TileSize));

    public (float X, float Y) ToWorld(TileCoord tile) =>
        (tile.X * TileSize, tile.Y * TileSize);

    public (float X, float Y) ToWorld(TileCoord tile, float progressX, float progressY) =>
        ((tile.X + progressX) * TileSize, (tile.Y + progressY) * TileSize);
}

namespace Genelife.Api.Models;

public sealed class MapDocument
{
    public string Id { get; set; } = "world";
    public int Version { get; set; }
    public int Seed { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public float TileSize { get; set; }
    public int MinX { get; set; }
    public int MinY { get; set; }
    public int MaxX { get; set; }
    public int MaxY { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

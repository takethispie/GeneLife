namespace Genelife.Api.DTOs;

/// <summary>
/// Static map layout sent once on connect and on demand. Tiles are
/// run-length encoded per row so a 256x256 map stays a small payload.
/// </summary>
public sealed class MapSnapshot
{
    public string MapId { get; set; } = string.Empty;
    public int Version { get; set; }
    public int Seed { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public float TileSize { get; set; }
    public int MinX { get; set; }
    public int MinY { get; set; }
    public int MaxX { get; set; }
    public int MaxY { get; set; }
    public List<MapTileRun> Tiles { get; set; } = new();
    public List<MapBuildingStatus> Buildings { get; set; } = new();
}

public sealed class MapTileRun
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Length { get; set; }
    public string Flags { get; set; } = string.Empty;
}

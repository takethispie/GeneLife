namespace Genelife.Api.DTOs;

/// <summary>
/// Static map layout sent once on connect and on demand. Tiles are
/// run-length encoded per row.
/// <para>
/// This is the whole-map, <c>O(area)</c> snapshot path, retained for the
/// 256-scale protocol and for old clients (versioned, not feature-flagged). At
/// 32000x32000 it is infeasible (~11 GB of runs), which is why the
/// viewport/chunk protocol (<c>MapChunk</c>, see
/// <c>docs/research/world-scale-migration-32000.md</c> §4.2) is the path the
/// client uses. Coordinates are <c>int32</c> tiles and <c>float32</c> world
/// units (exact to +/-16000 tiles / +/-1.02e6 px); the wire encodings are
/// specified by <c>MapChunkProtocol</c>.
/// </para>
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

namespace Genelife.Api.DTOs;

public sealed class MapDelta
{
    public string MapId { get; set; } = string.Empty;
    public int FromVersion { get; set; }
    public int ToVersion { get; set; }
    public long Tick { get; set; }
    public List<MapBuildingStatus> Added { get; set; } = new();
    public List<MapBuildingStatus> Updated { get; set; } = new();
    public List<ulong> Removed { get; set; } = new();
}

namespace Genelife.Web.Models;

public sealed class MapStatusData
{
    public string MapId { get; set; } = string.Empty;
    public int Version { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public float TileSize { get; set; }
    public int MinX { get; set; }
    public int MinY { get; set; }
    public int MaxX { get; set; }
    public int MaxY { get; set; }
    public int Buildings { get; set; }
    public int OccupiedTiles { get; set; }
    public int ReservedTiles { get; set; }
    public int UnplacedBuildings { get; set; }
    public int HealthIssues { get; set; }
    public List<MapBuildingData> BuildingList { get; set; } = new();
    public List<MapUnplacedBuildingData> Unplaced { get; set; } = new();
}

public sealed class MapUnplacedBuildingData
{
    public ulong NetworkId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class MapBuildingData
{
    public ulong NetworkId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int EntryX { get; set; }
    public int EntryY { get; set; }
    public ulong? OwnerNetworkId { get; set; }
}

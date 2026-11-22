namespace Genelife.Api.DTOs;

public sealed class MapStatus
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
    public List<MapBuildingStatus> BuildingList { get; set; } = new();
    public List<MapUnplacedBuildingStatus> Unplaced { get; set; } = new();
}

public sealed class MapUnplacedBuildingStatus
{
    public ulong NetworkId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class MapSummaryStatus
{
    public string MapId { get; set; } = string.Empty;
    public int Version { get; set; }
    public int Buildings { get; set; }
    public int OccupiedTiles { get; set; }
    public int UnplacedBuildings { get; set; }
    public int HealthIssues { get; set; }
}

public sealed class MapBuildingStatus
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

public sealed class MapTileStatus
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Flags { get; set; } = string.Empty;
}

public sealed class MapViewportStatus
{
    public int MinX { get; set; }
    public int MinY { get; set; }
    public int MaxX { get; set; }
    public int MaxY { get; set; }
    public List<MapBuildingStatus> Buildings { get; set; } = new();
    public List<MapTileStatus> Tiles { get; set; } = new();
}

public sealed class MapHealthStatus
{
    public int Issues { get; set; }
    public List<MapIssueStatus> Items { get; set; } = new();
}

public sealed class MapIssueStatus
{
    public ulong? NetworkId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? X { get; set; }
    public int? Y { get; set; }
}

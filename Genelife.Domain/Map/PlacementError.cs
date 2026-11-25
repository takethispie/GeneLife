namespace Genelife.Map;

public enum PlacementError
{
    None = 0,
    OutOfBounds,
    InvalidFootprint,
    Occupied,
    WrongZone,
    NoRoadAccess,
    EntryBlocked,
    NoAvailableLot,
    DuplicateNetworkId,
    LotReserved
}

public readonly record struct PlacementIssue(
    ulong? NetworkId,
    string Code,
    string Message,
    TileCoord? Tile);

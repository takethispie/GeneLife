using Genelife.Components.Gen;

namespace Genelife.Map;

public readonly record struct PlacementRequest(
    NetworkId BuildingId,
    BuildingKind Kind,
    Footprint Footprint,
    string District,
    TileCoord? DesiredAnchor = null,
    TileCoord? DesiredEntry = null)
{
    public static PlacementRequest At(
        NetworkId buildingId,
        BuildingKind kind,
        Footprint footprint,
        string district,
        TileCoord anchor,
        TileCoord? entry = null) =>
        new(buildingId, kind, footprint, district, anchor, entry);
}

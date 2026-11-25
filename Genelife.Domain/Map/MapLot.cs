namespace Genelife.Map;

public readonly record struct LotId(int Value)
{
    public override string ToString() => $"lot-{Value}";
}

public sealed class MapLot
{
    public LotId Id { get; init; }
    public MapBounds Bounds { get; init; }
    public MapZone Zone { get; set; }
    public bool IsReserved { get; set; }
    public ulong? ReservedBy { get; set; }

    public bool CanFit(Footprint footprint) =>
        footprint.IsValid &&
        footprint.Width <= Bounds.Width &&
        footprint.Height <= Bounds.Height;

    public bool Contains(Footprint footprint, TileCoord anchor) =>
        Bounds.Contains(MapBounds.FromAnchor(anchor, footprint));
}

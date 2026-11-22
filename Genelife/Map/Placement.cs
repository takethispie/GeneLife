namespace Genelife.Map;

/// <summary>
/// One authoritative placement for a building entity. Residential units and
/// employees inherit their building's placement rather than storing a copy.
/// </summary>
public record struct Placement
{
    public TileCoord Anchor;
    public Footprint Footprint;
    public TileCoord Entry;
    public BuildingKind Kind;
    public int Rotation;

    public Placement(
        TileCoord anchor,
        Footprint footprint,
        TileCoord entry,
        BuildingKind kind,
        int rotation = 0)
    {
        Anchor = anchor;
        Footprint = footprint;
        Entry = entry;
        Kind = kind;
        Rotation = rotation;
    }

    public readonly int MinX => Anchor.X;
    public readonly int MinY => Anchor.Y;
    public readonly int MaxX => Anchor.X + Footprint.Width - 1;
    public readonly int MaxY => Anchor.Y + Footprint.Height - 1;

    public readonly MapBounds Bounds => MapBounds.FromAnchor(Anchor, Footprint);

    public readonly IEnumerable<TileCoord> Tiles() => Footprint.Tiles(Anchor);

    public readonly bool Covers(TileCoord tile) =>
        tile.X >= MinX && tile.X <= MaxX && tile.Y >= MinY && tile.Y <= MaxY;
}

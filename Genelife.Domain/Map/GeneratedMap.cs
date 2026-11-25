namespace Genelife.Map;

public sealed class GeneratedMap
{
    public required MapOptions Options { get; init; }
    public required TileFlags[] Tiles { get; init; }
    public required List<MapLot> Lots { get; init; }
    public required List<TileCoord> SpawnPoints { get; init; }
    public required string CanonicalSignature { get; init; }

    public TileFlags FlagsAt(TileCoord tile)
    {
        var bounds = Options.Bounds;
        if (!bounds.Contains(tile))
            return TileFlags.None;

        var index = (tile.Y - bounds.MinY) * bounds.Width + (tile.X - bounds.MinX);
        return Tiles[index];
    }
}

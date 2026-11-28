namespace Genelife.Map;

public sealed class GeneratedMap
{
    public required MapOptions Options { get; init; }
    public required TileGrid Tiles { get; init; }
    public required List<MapLot> Lots { get; init; }
    public required List<TileCoord> SpawnPoints { get; init; }
    public required string CanonicalSignature { get; init; }

    /// <summary>
    /// The tile at a coordinate. Terrain is classified on demand (Rule 1), so
    /// this is the only supported way to read the generated world.
    /// </summary>
    public TileFlags FlagsAt(TileCoord tile) => Tiles[tile];
}

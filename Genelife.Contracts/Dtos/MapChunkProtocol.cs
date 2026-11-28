namespace Genelife.Api.DTOs;

/// <summary>
/// The single shared definition of the viewport-snapshot chunk grid.
///
/// Both sides of the wire read their chunk size and their chunk→tile transform
/// from here: the server builds chunk payloads with <see cref="ChunkSize"/>, and
/// a second implementation (the Godot client, the browser client) mirrors the
/// same constant rather than keeping its own literal. If the server shipped
/// 64-tile chunks and a client cached 32-tile chunks, every load would be a
/// re-slice, so the value is owned by <c>Genelife.Contracts</c> and never
/// restated (docs/research/world-scale-migration-32000.md §4.4).
///
/// The grid is anchored at the world's <em>min corner</em>, not the origin:
/// chunk <c>(0, 0)</c> starts at <c>(MapOptions.Bounds.MinX, MinY)</c>. The
/// world is origin-centred, so at 32000×32000 that corner is <c>(-16000, -16000)</c>.
///
/// <para><b>Wire encodings</b> (documented here because the client is a second
/// implementation — E-4):</para>
/// <list type="bullet">
/// <item><description>tile coordinates (<c>MapTileRun.X/Y</c>, <c>TileCoord</c>,
/// a chunk address, a manifest extent) are <c>int32</c>. ±16000 sits just inside
/// <c>int16</c>'s 32767 with no margin, so there is deliberately no
/// <c>short</c> packing.</description></item>
/// <item><description>world/pixel coordinates are <c>float32</c> (exact to
/// 2²⁴ ≈ 1.67e7, which covers ±1.02e6 px at 32 px/tile).</description></item>
/// <item><description>a tile run is <c>{ X, Y, Length, Flags }</c> — a horizontal
/// span of <c>Length</c> tiles starting at <c>(X, Y)</c>, <c>Flags</c> a
/// comma-separated list of <c>TileFlags</c> names (<c>Land</c>, <c>Road</c>,
/// <c>Occupied</c>, <c>Reserved</c>, <c>Water</c>). The run's origin is
/// world-tile for the whole-map snapshot and chunk-local inside a chunk.</description></item>
/// </list>
/// </summary>
public static class MapChunkProtocol
{
    /// <summary>Tiles per chunk on each axis, identical on both sides of the wire.</summary>
    public const int ChunkSize = 64;

    /// <summary>
    /// Version of the RLE tile encoding carried by a chunk payload. Bump it when
    /// the wire shape of a run changes; a client that does not recognise the
    /// value refuses the payload cleanly instead of mis-decoding it.
    /// </summary>
    public const int TileEncodingVersion = 1;

    /// <summary>Chunks needed to cover a world <paramref name="extent"/> tiles wide.</summary>
    public static int ChunksPerSide(int extent) =>
        extent <= 0 ? 0 : (extent + ChunkSize - 1) / ChunkSize;

    /// <summary>World tile coordinate of the chunk's min corner on one axis.</summary>
    public static int ChunkOrigin(int worldMin, int chunk) => worldMin + chunk * ChunkSize;

    /// <summary>
    /// The world tile at a chunk's min corner. <c>tileX = MinX + cx * ChunkSize</c>.
    /// The world's minimum corner is the grid's origin, so chunk <c>(0, 0)</c>
    /// starts at <c>MinX, MinY</c> — not at world tile <c>(0, 0)</c>.
    /// </summary>
    public static (int X, int Y) ChunkToTile(int cx, int cy, int worldMinX, int worldMinY) =>
        (ChunkOrigin(worldMinX, cx), ChunkOrigin(worldMinY, cy));

    /// <summary>The chunk address that contains a world tile. Floor division, so it is defined for negative tiles.</summary>
    public static (int Cx, int Cy) TileToChunk(int tileX, int tileY, int worldMinX, int worldMinY) =>
        (FloorDiv(tileX - worldMinX, ChunkSize), FloorDiv(tileY - worldMinY, ChunkSize));

    /// <summary>
    /// The world-tile bounds of one chunk, clamped to the world's own bounds so a
    /// partial chunk at the far edge never reports tiles outside the world.
    /// </summary>
    public static (int MinX, int MinY, int MaxX, int MaxY) ChunkTileBounds(
        int cx,
        int cy,
        int worldMinX,
        int worldMinY,
        int worldMaxX,
        int worldMaxY)
    {
        var minX = ChunkOrigin(worldMinX, cx);
        var minY = ChunkOrigin(worldMinY, cy);
        return (
            minX,
            minY,
            Math.Min(minX + ChunkSize - 1, worldMaxX),
            Math.Min(minY + ChunkSize - 1, worldMaxY));
    }

    private static int FloorDiv(int value, int divisor)
    {
        var quotient = value / divisor;
        if (value % divisor != 0 && (value < 0) != (divisor < 0))
            quotient--;
        return quotient;
    }
}

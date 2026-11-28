namespace Genelife.Api.DTOs;

/// <summary>
/// The world manifest: fetched once per session and cached by the client.
///
/// It is the client's single source for the world extent and the encoding
/// version — the client must not keep its own extent or chunk-size literal. A
/// change in <see cref="SpecHash"/> means the world was regenerated, and the
/// client treats it as a full chunk-cache purge (its cache key is
/// <c>(cx, cy, specHash)</c>).
/// </summary>
public sealed class MapManifest
{
    /// <summary>False until a world has been generated; every other field is then meaningless.</summary>
    public bool HasMap { get; set; }

    /// <summary>
    /// Signature of the generator SPEC (seed + options), not a hash of the tiles.
    /// Stable across processes and machines for the same inputs, so it identifies
    /// the world in a few dozen bytes where hashing 1.02e9 tiles could not.
    /// </summary>
    public string SpecHash { get; set; } = string.Empty;

    /// <summary>World extent in TILES.</summary>
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>Tiles per chunk — <see cref="MapChunkProtocol.ChunkSize"/>.</summary>
    public int ChunkSize { get; set; } = MapChunkProtocol.ChunkSize;

    /// <summary>Version of the RLE tile encoding — <see cref="MapChunkProtocol.TileEncodingVersion"/>.</summary>
    public int TileEncodingVersion { get; set; } = MapChunkProtocol.TileEncodingVersion;

    /// <summary>The world's inclusive tile bounds (origin-centred, so <c>MinX</c> is negative for a centred world).</summary>
    public int MinX { get; set; }
    public int MinY { get; set; }
    public int MaxX { get; set; }
    public int MaxY { get; set; }

    /// <summary>Chunk-grid dimensions; a chunk address is valid while <c>0 &lt;= cx &lt; ChunksPerSideX</c>.</summary>
    public int ChunksPerSideX { get; set; }
    public int ChunksPerSideY { get; set; }
}

/// <summary>
/// One chunk of tiles, addressed by <see cref="Cx"/>, <see cref="Cy"/>.
///
/// <see cref="Tiles"/> are run-length encoded with the same per-row run format as
/// the whole-map snapshot, but in <em>chunk-local</em> tile coordinates
/// (<c>0 .. ChunkSize-1</c>) so a payload is self-contained and its bytes are a
/// pure function of <c>(specHash, cx, cy)</c>. Convert a run tile back to a world
/// tile with <c>worldX = MinX + Cx*ChunkSize + run.X</c>.
/// </summary>
public sealed class MapChunk
{
    public int Cx { get; set; }
    public int Cy { get; set; }
    public List<MapTileRun> Tiles { get; set; } = new();
}

/// <summary>
/// One viewport's worth of chunks: the answer to a single batched request.
///
/// A viewport spans many chunks, so the protocol answers a chunk SET per request
/// rather than one message per chunk. <see cref="MinX"/>..<see cref="MaxY"/> are
/// the world tile bounds the client asked for; the tiles and
/// <see cref="Buildings"/> are scoped by that one rect, so the client never sees
/// a building whose tile is missing or a tile whose occupant is missing (E-7).
///
/// <see cref="SpecHash"/> is repeated on every response so a mid-session
/// regeneration is detectable without a manifest round trip; the client drops
/// its cache when it changes.
/// </summary>
public sealed class MapChunkBatch
{
    public string SpecHash { get; set; } = string.Empty;
    public int TileEncodingVersion { get; set; } = MapChunkProtocol.TileEncodingVersion;
    public int ChunkSize { get; set; } = MapChunkProtocol.ChunkSize;
    public int MinX { get; set; }
    public int MinY { get; set; }
    public int MaxX { get; set; }
    public int MaxY { get; set; }
    public List<MapChunk> Chunks { get; set; } = new();
    public List<MapBuildingStatus> Buildings { get; set; } = new();
}

/// <summary>
/// The coarse world overview (Tier A): one dominant-terrain code per chunk,
/// delivered by its own endpoint and fetched once when the map opens.
///
/// A client cannot derive a cartogram from 1.02e9 tiles, so this is a protocol
/// requirement, not a nicety. <see cref="Dominant"/> is the chunk-grid raster in
/// row-major RLE — one <see cref="MapOverviewRun"/> covers a horizontal span of
/// chunks on one <see cref="MapOverviewRun.Row"/>. Uncompressed it is
/// <c>ChunksPerSideX × ChunksPerSideY</c> bytes (about 250 KB at 32000×32000,
/// 0.02% of the map).
///
/// It is stable for a given <see cref="SpecHash"/>: it is derived from the static
/// terrain, not from building occupancy.
/// </summary>
public sealed class MapOverview
{
    public string SpecHash { get; set; } = string.Empty;
    public int ChunkSize { get; set; } = MapChunkProtocol.ChunkSize;
    public int Width { get; set; }
    public int Height { get; set; }
    public int Columns { get; set; }
    public int Rows { get; set; }
    public List<MapOverviewRun> Dominant { get; set; } = new();
}

/// <summary>One horizontal run of equal dominant terrain on the chunk grid.</summary>
public sealed class MapOverviewRun
{
    /// <summary>Chunk row (<c>cy</c>).</summary>
    public int Row { get; set; }

    /// <summary>Starting chunk column (<c>cx</c>).</summary>
    public int Start { get; set; }

    /// <summary>Number of chunks covered.</summary>
    public int Length { get; set; }

    /// <summary>Dominant terrain: <c>Land</c>, <c>Road</c> or <c>Water</c>.</summary>
    public string Terrain { get; set; } = string.Empty;
}

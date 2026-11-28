using System.Text.Json;
using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Genelife.Housing;
using Genelife.Map;

namespace Genelife.UnitTests.Map;

/// <summary>
/// Guards the viewport snapshot protocol: the chunk grid is a clean division of
/// the world, a chunk's runs cover its tiles exactly and are byte-stable for a
/// given <c>(specHash, cx, cy)</c>, the manifest carries the extent and encoding
/// version, and the overview raster covers the whole chunk grid.
/// </summary>
public class MapChunkProtocolTests
{
    private static SimulationEngine NewEngine()
    {
        var engine = new SimulationEngine();
        engine.Initialize();
        return engine;
    }

    [Fact]
    public void Chunk_size_divides_the_32000_world_cleanly()
    {
        Assert.Equal(500, MapChunkProtocol.ChunksPerSide(32000));
        Assert.Equal(4, MapChunkProtocol.ChunksPerSide(256));
        Assert.Equal(64, MapChunkProtocol.ChunkSize);
    }

    [Fact]
    public void Chunk_transform_anchors_at_the_world_min_corner()
    {
        // Origin-centred world: the min corner is -16000, so chunk (0,0) starts
        // there — not at world tile (0,0).
        Assert.Equal((-16000, -15936), MapChunkProtocol.ChunkToTile(0, 1, -16000, -16000));
        Assert.Equal((15936, 15936), MapChunkProtocol.ChunkToTile(499, 499, -16000, -16000));
        Assert.Equal((0, 1), MapChunkProtocol.TileToChunk(-16000, -16000 + 64, -16000, -16000));
        // Floor division: the tile just left of the world min lands in chunk -1
        // (outside the grid), never in chunk 0.
        Assert.Equal((-1, 2), MapChunkProtocol.TileToChunk(-16000 - 1, -16000 + 128, -16000, -16000));
        Assert.Equal((0, 0), MapChunkProtocol.TileToChunk(-16000, -16000, -16000, -16000));
    }

    [Fact]
    public void Chunk_bounds_are_clamped_to_the_world()
    {
        var (minX, minY, maxX, maxY) = MapChunkProtocol.ChunkTileBounds(499, 499, -16000, -16000, 15999, 15999);
        Assert.Equal((15936, 15936, 15999, 15999), (minX, minY, maxX, maxY));
    }

    [Fact]
    public void Manifest_describes_extent_chunk_grid_and_encoding_version()
    {
        using var engine = NewEngine();
        var manifest = engine.BuildMapManifest();
        var options = engine.MapContext!.Options;
        var bounds = options.Bounds;

        Assert.True(manifest.HasMap);
        Assert.Equal(options.Width, manifest.Width);
        Assert.Equal(options.Height, manifest.Height);
        Assert.Equal(MapChunkProtocol.ChunkSize, manifest.ChunkSize);
        Assert.Equal(MapChunkProtocol.TileEncodingVersion, manifest.TileEncodingVersion);
        Assert.Equal(MapChunkProtocol.ChunksPerSide(options.Width), manifest.ChunksPerSideX);
        Assert.Equal(MapChunkProtocol.ChunksPerSide(options.Height), manifest.ChunksPerSideY);
        Assert.Equal(bounds.MinX, manifest.MinX);
        Assert.Equal(bounds.MinY, manifest.MinY);
        Assert.Equal(bounds.MaxX, manifest.MaxX);
        Assert.Equal(bounds.MaxY, manifest.MaxY);
        Assert.Equal(engine.MapContext.Generated.CanonicalSignature, manifest.SpecHash);
    }

    [Fact]
    public void Manifest_reports_no_map_before_generation()
    {
        using var engine = new SimulationEngine();
        var manifest = engine.BuildMapManifest();
        Assert.False(manifest.HasMap);
        Assert.Null(engine.BuildMapChunks(0, 0, 0, 0));
        Assert.Null(engine.BuildMapOverview());
    }

    [Fact]
    public void Chunk_runs_cover_the_chunk_exactly_and_match_the_layout()
    {
        using var engine = NewEngine();
        var layout = engine.MapContext!.Map.Layout!;
        var bounds = engine.MapContext.Options.Bounds;

        var batch = engine.BuildMapChunks(0, 0, 0, 0)!;
        var chunk = Assert.Single(batch.Chunks);
        Assert.Equal(0, chunk.Cx);
        Assert.Equal(0, chunk.Cy);

        var flags = new Dictionary<(int X, int Y), string>();
        foreach (var run in chunk.Tiles)
        {
            Assert.InRange(run.Length, 1, MapChunkProtocol.ChunkSize);
            Assert.InRange(run.X, 0, MapChunkProtocol.ChunkSize - 1);
            Assert.InRange(run.Y, 0, MapChunkProtocol.ChunkSize - 1);
            for (var offset = 0; offset < run.Length; offset++)
                flags[(run.X + offset, run.Y)] = run.Flags;
        }

        Assert.Equal(
            MapChunkProtocol.ChunkSize * MapChunkProtocol.ChunkSize,
            chunk.Tiles.Sum(run => run.Length));

        for (var y = 0; y < MapChunkProtocol.ChunkSize; y++)
        {
            for (var x = 0; x < MapChunkProtocol.ChunkSize; x++)
            {
                Assert.True(flags.TryGetValue((x, y), out var actual), $"Missing tile {x},{y}");
                Assert.Equal(layout.FlagsAt(new TileCoord(bounds.MinX + x, bounds.MinY + y)).ToString(), actual);
            }
        }
    }

    [Fact]
    public void A_multi_chunk_batch_contains_one_entry_per_address()
    {
        using var engine = NewEngine();
        var batch = engine.BuildMapChunks(1, 1, 2, 2)!;
        Assert.Equal(4, batch.Chunks.Count);
        Assert.Equal(
            4,
            batch.Chunks.Select(chunk => (chunk.Cx, chunk.Cy)).Distinct().Count());
        Assert.Contains(batch.Chunks, chunk => chunk.Cx == 1 && chunk.Cy == 1);
        Assert.Contains(batch.Chunks, chunk => chunk.Cx == 2 && chunk.Cy == 2);
    }

    [Fact]
    public void Chunk_payload_is_byte_stable_for_a_spec_and_address()
    {
        using var engine = NewEngine();
        var first = JsonSerializer.Serialize(engine.BuildMapChunks(1, 1, 2, 2));
        var second = JsonSerializer.Serialize(engine.BuildMapChunks(1, 1, 2, 2));
        Assert.Equal(first, second);
    }

    [Fact]
    public void Out_of_range_chunk_address_is_rejected()
    {
        using var engine = NewEngine();
        var manifest = engine.BuildMapManifest();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            engine.BuildMapChunks(0, 0, manifest.ChunksPerSideX, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            engine.BuildMapChunks(0, 0, 0, manifest.ChunksPerSideY));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            engine.BuildMapChunks(-1, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            engine.BuildMapChunks(1, 0, 0, 0));
    }

    [Fact]
    public void Chunk_batch_scopes_tiles_and_buildings_to_one_rect()
    {
        using var engine = NewEngine();
        var world = engine.World!;
        var map = engine.MapContext!;
        HousingBootstrap.CreateStock(world, engine.Economy!, 0, map: map);

        var batch = engine.BuildMapChunks(1, 1, 1, 1)!;
        var rect = new MapBounds(batch.MinX, batch.MinY, batch.MaxX, batch.MaxY);
        Assert.Equal(map.Options.Bounds.MinX + MapChunkProtocol.ChunkSize, batch.MinX);

        var intersecting = map.Map.Placements.Values
            .Where(placement => placement.Bounds.Intersects(rect))
            .ToList();

        Assert.Equal(intersecting.Count, batch.Buildings.Count);
        foreach (var building in batch.Buildings)
        {
            var footprint = new MapBounds(
                building.X, building.Y,
                building.X + building.Width - 1, building.Y + building.Height - 1);
            Assert.True(footprint.Intersects(rect), $"Building {building.NetworkId} is outside the requested rect.");
        }
    }

    [Fact]
    public void Overview_raster_covers_every_chunk_and_carries_the_manifest_spec()
    {
        using var engine = NewEngine();
        var manifest = engine.BuildMapManifest();
        var overview = engine.BuildMapOverview()!;

        Assert.Equal(manifest.SpecHash, overview.SpecHash);
        Assert.Equal(manifest.ChunksPerSideX, overview.Columns);
        Assert.Equal(manifest.ChunksPerSideY, overview.Rows);
        Assert.Equal(MapChunkProtocol.ChunkSize, overview.ChunkSize);

        var cells = new Dictionary<(int Cx, int Cy), string>();
        foreach (var run in overview.Dominant)
        {
            Assert.InRange(run.Length, 1, overview.Columns);
            for (var offset = 0; offset < run.Length; offset++)
                cells[(run.Start + offset, run.Row)] = run.Terrain;
        }

        Assert.Equal(overview.Columns * overview.Rows, cells.Count);
        foreach (var terrain in cells.Values)
            Assert.Contains(terrain, new[] { "Land", "Road", "Water", "None" });
    }

    [Fact]
    public void Overview_is_stable_across_calls()
    {
        using var engine = NewEngine();
        var first = JsonSerializer.Serialize(engine.BuildMapOverview());
        var second = JsonSerializer.Serialize(engine.BuildMapOverview());
        Assert.Equal(first, second);
    }

    [Fact]
    public void Overview_dominant_matches_the_majority_terrain_of_each_chunk()
    {
        using var engine = NewEngine();
        var layout = engine.MapContext!.Map.Layout!;
        var bounds = engine.MapContext.Options.Bounds;
        var overview = engine.BuildMapOverview()!;

        var dominant = new Dictionary<(int Cx, int Cy), string>();
        foreach (var run in overview.Dominant)
        {
            for (var offset = 0; offset < run.Length; offset++)
                dominant[(run.Start + offset, run.Row)] = run.Terrain;
        }

        for (var cy = 0; cy < overview.Rows; cy++)
        {
            for (var cx = 0; cx < overview.Columns; cx++)
            {
                var (minX, minY, maxX, maxY) = MapChunkProtocol.ChunkTileBounds(
                    cx, cy, bounds.MinX, bounds.MinY, bounds.MaxX, bounds.MaxY);

                long land = 0, road = 0, water = 0;
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        var flags = layout.FlagsAt(new TileCoord(x, y));
                        if ((flags & TileFlags.Water) != 0) water++;
                        else if ((flags & TileFlags.Road) != 0) road++;
                        else land++;
                    }
                }

                var expected = water >= road && water >= land ? "Water"
                    : road >= land ? "Road"
                    : "Land";

                Assert.Equal(expected, dominant[(cx, cy)]);
            }
        }
    }
}

using System.Security.Cryptography;
using Genelife.Map;

namespace Genelife.UnitTests.Map;

/// <summary>
/// Pins the generated world bit-for-bit across the Phase 1 rewrite
/// (<c>docs/research/world-scale-migration-32000.md</c> §5.2). The generator
/// changed *how* the world is produced — a coordinate is now classified on
/// demand instead of written into a dense array — but not *what* it produces.
///
/// The digests below were captured from the dense generator immediately before
/// the rewrite, so a failure here means the lazy classifier changed the world
/// and the migration is no longer behaviour-preserving.
/// </summary>
public class MapGenerationGoldenTests
{
    /// <summary>Row-major SHA-256 over the tile flags — the world's output, independent of how it is stored.</summary>
    private static string TileDigest(GeneratedMap map)
    {
        var bounds = map.Options.Bounds;
        var bytes = new byte[bounds.Area];
        var index = 0;
        for (var y = bounds.MinY; y <= bounds.MaxY; y++)
        for (var x = bounds.MinX; x <= bounds.MaxX; x++)
            bytes[index++] = (byte)map.FlagsAt(new TileCoord(x, y));

        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    [Fact]
    public void Default_256_world_is_unchanged()
    {
        var map = new MapGenerator().Generate(MapOptions.Default, MapGenerationOptions.Default);

        Assert.Equal(
            "3D6346DD7864017C1A628FC2B528AE454872195DE2F4F5EA9D5D4A63407EA1B1",
            TileDigest(map));
        Assert.Equal(400, map.Lots.Count);
        Assert.Equal(9, map.SpawnPoints.Count);
    }

    [Fact]
    public void Small_world_with_custom_generation_options_is_unchanged()
    {
        var map = new MapGenerator().Generate(
            new MapOptions { Seed = 7, Width = 64, Height = 64 },
            new MapGenerationOptions { RoadSpacing = 10, WaterFraction = 0.01f });

        Assert.Equal(
            "5B666E99C9439611AE047C31105F8395400429066C839E273D87AE3E44172C9F",
            TileDigest(map));
        Assert.Equal(36, map.Lots.Count);
        Assert.Equal(9, map.SpawnPoints.Count);
    }

    [Fact]
    public void Small_world_with_default_generation_options_is_unchanged()
    {
        var map = new MapGenerator().Generate(new MapOptions { Seed = 42, Width = 64, Height = 64 });

        Assert.Equal(
            "3E4631B2EDEEA657E97F6904C921A03AF328C36063E517BD73925A4ED3046D9A",
            TileDigest(map));
        Assert.Equal(16, map.Lots.Count);
    }

    [Fact]
    public void Water_free_world_is_unchanged()
    {
        var map = new MapGenerator().Generate(
            new MapOptions { Seed = 3, Width = 32, Height = 32 },
            new MapGenerationOptions { WaterFraction = 0f, RoadSpacing = 8 });

        Assert.Equal(
            "6C3783DE8F1C5C958E6084897CEC6A994E5B2A211BA86AD37273D169989C4BFD",
            TileDigest(map));
        Assert.Equal(9, map.Lots.Count);
    }

    /// <summary>
    /// The signature is a hash of the generator SPEC, not of the tiles — that is
    /// what makes it a few dozen bytes instead of gigabytes. Pinning the value is
    /// deliberate: <see cref="MapGenerator.GeneratorVersion"/> is part of the
    /// spec, so a change to the algorithm or the spec format is meant to move
    /// this constant (and its version) on purpose, never silently reuse a
    /// signature that would then describe two different worlds.
    /// </summary>
    [Fact]
    public void Signature_is_a_pure_function_of_the_generator_spec()
    {
        var map = new MapGenerator().Generate(MapOptions.Default, MapGenerationOptions.Default);

        Assert.Equal(
            "31BA8602D6F3BE79FF30D540F4F4EC8ED6C8C3A6B014C70C75392809CE523E34",
            map.CanonicalSignature);
    }

    /// <summary>
    /// The correctness gate for the 32000×32000 ruling: generation must be a
    /// pure function of the coordinate, so a world far larger than any dense
    /// array could hold is classified on demand. <c>RoadSpacing = 32000</c> keeps
    /// the lot list tiny on purpose — the point under test is that terrain no
    /// longer scales with area, not the lot pitch.
    /// </summary>
    [Fact]
    public void Generation_is_a_pure_function_at_32000x32000()
    {
        var map = new MapGenerator().Generate(
            new MapOptions { Width = 32000, Height = 32000 },
            new MapGenerationOptions { RoadSpacing = 32000, WaterFraction = 0f });

        Assert.Equal(32000 * 32000, map.Tiles.Count);
        Assert.Equal(TileFlags.Land | TileFlags.Road, map.FlagsAt(new TileCoord(0, 0)));
        Assert.Equal(TileFlags.Land, map.FlagsAt(new TileCoord(1, 1)));
        Assert.Equal(TileFlags.None, map.FlagsAt(new TileCoord(32000, 0)));
    }
}

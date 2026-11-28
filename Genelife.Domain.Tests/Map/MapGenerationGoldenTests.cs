using System.Security.Cryptography;
using Genelife.Map;

namespace Genelife.UnitTests.Map;

/// <summary>
/// Pins the generated world bit-for-bit. The generator changed *how* the world
/// is produced — a coordinate is now classified on demand instead of written
/// into a dense array (<c>docs/research/world-scale-migration-32000.md</c> §5.2)
/// — while the phase-1 rewrite itself was behaviour-preserving.
///
/// <para>
/// These digests were then re-pinned once, deliberately, by the building-sizing
/// migration (<c>docs/research/building-sizing-and-world-scale.md</c>): the
/// default <see cref="MapGenerationOptions.RoadSpacing"/> rose from 12 to 24 so a
/// lot can contain a properly-sized footprint, and water is no longer placed
/// inside a lot interior (a dry parcel). Both change the world on purpose, so
/// <see cref="MapGenerator.GeneratorVersion"/> was bumped to 3 in the same change
/// and the pins below follow it. A failure here now means the generator moved
/// again without the version (or this pin) moving with it.
/// </para>
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
            "86454ED7400118DBD35C72F6590A82F5305B0EC1E1A4582DD4322D31FA055B6B",
            TileDigest(map));
        Assert.Equal(100, map.Lots.Count);
        Assert.Equal(9, map.SpawnPoints.Count);
    }

    [Fact]
    public void Small_world_with_custom_generation_options_is_unchanged()
    {
        var map = new MapGenerator().Generate(
            new MapOptions { Seed = 7, Width = 64, Height = 64 },
            new MapGenerationOptions { RoadSpacing = 10, WaterFraction = 0.01f });

        Assert.Equal(
            "F7EDCC87C14F339E387D58CE0D968F0342FB1A0F5611E0F0F6FD7F906E0777F7",
            TileDigest(map));
        Assert.Equal(36, map.Lots.Count);
        Assert.Equal(9, map.SpawnPoints.Count);
    }

    [Fact]
    public void Small_world_with_default_generation_options_is_unchanged()
    {
        var map = new MapGenerator().Generate(new MapOptions { Seed = 42, Width = 64, Height = 64 });

        Assert.Equal(
            "7520FDFB163F0D9F9BC5CF6DE4E4E588723E211A821F96D2A3F55BD92C9DBC4D",
            TileDigest(map));
        Assert.Equal(4, map.Lots.Count);
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
            "F7BC46A66631A9D2925E3499986FD0C15957691A82E8D8D6BA64C99C49336DA6",
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

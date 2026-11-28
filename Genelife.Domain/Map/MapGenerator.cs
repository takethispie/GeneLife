using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Genelife.Map;

/// <summary>
/// Deterministic map structure generator. Produces terrain, roads, lots and
/// spawn points; never creates buildings.
///
/// Generation is a pure function of the coordinate (Rule 1 of
/// <c>docs/research/world-scale-migration-32000.md</c> §4.1): the generator no
/// longer materialises a dense <c>TileFlags[width * height]</c> grid, so a world
/// can be classified for a coordinate without the world existing as an array.
/// The only per-world state is the sparse water mask, itself a deterministic
/// function of the seed and generation options; every other tile decides itself
/// from its coordinate.
/// </summary>
public sealed class MapGenerator
{
    /// <summary>
    /// Generator algorithm version.<br/>
    /// This is part of the world's canonical signature, so it MUST be bumped
    /// whenever <see cref="Generate"/> (via <see cref="Classify"/> or the water
    /// mask) starts producing a different tile for a coordinate. A signature is
    /// only sound while a deterministic function's identity is exactly its
    /// inputs, so leaving this constant behind would let two different worlds
    /// share a signature that lies. Bump it — never recompute the tiles into the
    /// signature to "prove" a change.
    /// </summary>
    public const int GeneratorVersion = 2;

    public GeneratedMap Generate(MapOptions options, MapGenerationOptions? generationOptions = null)
    {
        generationOptions ??= MapGenerationOptions.Default;
        var bounds = options.Bounds;

        var water = BuildWaterMask(bounds, generationOptions, options.Seed);
        var tiles = TileGrid.Lazy(bounds, tile => Classify(tile, bounds, generationOptions, water));
        var lots = BuildLots(bounds, generationOptions);
        AssignZones(lots, generationOptions);
        var spawns = BuildSpawnPoints(tiles, bounds);
        var signature = ComputeSignature(options, generationOptions);

        return new GeneratedMap
        {
            Options = options,
            Tiles = tiles,
            Lots = lots,
            SpawnPoints = spawns,
            CanonicalSignature = signature
        };
    }

    /// <summary>
    /// The single tile classifier. Terrain is land unless the tile is water (a
    /// deterministic pick from the seed) or falls on a road line (and is not
    /// water). This reproduces the historical land → water → road carve order
    /// exactly, so at 256×256 the generated world is bit-for-bit what the dense
    /// generator produced.
    /// </summary>
    private static TileFlags Classify(
        TileCoord tile,
        MapBounds bounds,
        MapGenerationOptions options,
        HashSet<long> water)
    {
        if (!bounds.Contains(tile))
            return TileFlags.None;

        if (water.Contains(Key(tile.X, tile.Y)))
            return TileFlags.Water;

        var spacing = Math.Max(4, options.RoadSpacing);
        if (Mod(tile.X, spacing) == 0 || Mod(tile.Y, spacing) == 0)
            return TileFlags.Land | TileFlags.Road;

        return TileFlags.Land;
    }

    /// <summary>
    /// Reproduces the historical water fill deterministically, but stores the
    /// chosen tiles in a sparse set instead of a world-sized flag array. The
    /// draw sequence, the centre exclusion and the attempt budget are unchanged,
    /// so the water a coordinate reports is identical to the dense generator's.
    /// </summary>
    private static HashSet<long> BuildWaterMask(MapBounds bounds, MapGenerationOptions options, int seed)
    {
        var water = new HashSet<long>();
        if (options.WaterFraction <= 0f)
            return water;

        var waterTiles = (int)(bounds.Area * options.WaterFraction);
        if (waterTiles <= 0)
            return water;

        var random = new SimulationRandom(seed).For("map-generation");
        var placed = 0;
        var attempts = 0;
        while (placed < waterTiles && attempts++ < waterTiles * 20)
        {
            var x = random.Next(bounds.MinX, bounds.MaxX + 1);
            var y = random.Next(bounds.MinY, bounds.MaxY + 1);
            if (Math.Abs(x) < bounds.Width / 8 && Math.Abs(y) < bounds.Height / 8)
                continue;

            if (water.Add(Key(x, y)))
                placed++;
        }

        return water;
    }

    private static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;

    private static List<MapLot> BuildLots(MapBounds bounds, MapGenerationOptions options)
    {
        var lots = new List<MapLot>();
        var spacing = Math.Max(4, options.RoadSpacing);
        var nextId = 1;

        var firstX = FirstMultipleAtOrAfter(bounds.MinX, spacing);
        var firstY = FirstMultipleAtOrAfter(bounds.MinY, spacing);

        for (var roadX = firstX; roadX + spacing <= bounds.MaxX; roadX += spacing)
        {
            for (var roadY = firstY; roadY + spacing <= bounds.MaxY; roadY += spacing)
            {
                var lotBounds = new MapBounds(
                    roadX + 1,
                    roadY + 1,
                    roadX + spacing - 1,
                    roadY + spacing - 1);

                if (lotBounds.Area < options.MinLotTiles)
                    continue;
                if (lotBounds.MaxX > bounds.MaxX || lotBounds.MaxY > bounds.MaxY)
                    continue;

                lots.Add(new MapLot
                {
                    Id = new LotId(nextId++),
                    Bounds = lotBounds,
                    Zone = MapZone.Mixed
                });
            }
        }

        return lots;
    }

    private static int FirstMultipleAtOrAfter(int value, int modulus)
    {
        var remainder = Mod(value, modulus);
        return remainder == 0 ? value : value + (modulus - remainder);
    }

    private static void AssignZones(List<MapLot> lots, MapGenerationOptions options)
    {
        if (lots.Count == 0)
            return;

        var total = Math.Max(1, options.ResidentialShare + options.CommercialShare +
                                options.EducationShare + options.CivicShare + 20);
        var residentialEnd = lots.Count * options.ResidentialShare / total;
        var commercialEnd = residentialEnd + lots.Count * options.CommercialShare / total;
        var educationEnd = commercialEnd + lots.Count * options.EducationShare / total;
        var civicEnd = educationEnd + lots.Count * options.CivicShare / total;

        for (var i = 0; i < lots.Count; i++)
        {
            lots[i].Zone = i < residentialEnd ? MapZone.Residential
                : i < commercialEnd ? MapZone.Commercial
                : i < educationEnd ? MapZone.Education
                : i < civicEnd ? MapZone.Civic
                : MapZone.Mixed;
        }
    }

    private static List<TileCoord> BuildSpawnPoints(TileGrid tiles, MapBounds bounds)
    {
        var spawns = new List<TileCoord>();
        var center = new TileCoord(0, 0);
        if (IsWalkable(tiles[center]))
            spawns.Add(center);

        for (var radius = 1; radius <= Math.Max(bounds.Width, bounds.Height) && spawns.Count < 8; radius++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                for (var x = -radius; x <= radius; x++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(y)) != radius)
                        continue;
                    var tile = new TileCoord(x, y);
                    if (!bounds.Contains(tile))
                        continue;
                    if (IsWalkable(tiles[tile]) && !spawns.Contains(tile))
                        spawns.Add(tile);
                }
            }
        }

        return spawns;
    }

    /// <summary>
    /// Hashes the generator SPEC, not its output. A deterministic function's
    /// identity is its inputs, so <c>SHA256(MapId | GeneratorVersion | seed |
    /// size | generation params)</c> identifies the world in a few dozen bytes —
    /// where hashing the tiles would need gigabytes of text at 32000×32000.
    /// The same spec string is hashed for the same inputs, so the value is
    /// stable across processes and machines.
    /// </summary>
    private static string ComputeSignature(MapOptions options, MapGenerationOptions generation)
    {
        var spec = new StringBuilder()
            .Append("MapGenerator|").Append(GeneratorVersion).Append('|')
            .Append(options.MapId).Append('|')
            .Append(options.Version).Append('|')
            .Append(options.Seed).Append('|')
            .Append(options.Width).Append('|')
            .Append(options.Height).Append('|')
            .Append(options.TileSize.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(generation.RoadSpacing).Append('|')
            .Append(generation.BlockPadding).Append('|')
            .Append(generation.WaterFraction.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(generation.MinLotTiles).Append('|')
            .Append(generation.ResidentialShare).Append('|')
            .Append(generation.CommercialShare).Append('|')
            .Append(generation.EducationShare).Append('|')
            .Append(generation.CivicShare);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(spec.ToString())));
    }

    private static bool IsWalkable(TileFlags flags) =>
        (flags & (TileFlags.Water | TileFlags.Occupied | TileFlags.Reserved)) == 0 &&
        (flags & (TileFlags.Land | TileFlags.Road)) != 0;

    private static int Mod(int value, int modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}

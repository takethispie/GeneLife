using System.Security.Cryptography;
using System.Text;

namespace Genelife.Map;

/// <summary>
/// Deterministic map structure generator. Produces terrain, roads, lots and
/// spawn points; never creates buildings.
/// </summary>
public sealed class MapGenerator
{
    private static readonly MapZone[] ZoneOrder =
    {
        MapZone.Residential,
        MapZone.Commercial,
        MapZone.Education,
        MapZone.Civic,
        MapZone.Mixed
    };

    public GeneratedMap Generate(MapOptions options, MapGenerationOptions? generationOptions = null)
    {
        generationOptions ??= MapGenerationOptions.Default;
        var bounds = options.Bounds;
        var tiles = new TileFlags[Math.Max(1, bounds.Area)];
        var random = new SimulationRandom(options.Seed).For("map-generation");

        FillLand(tiles);
        CarveWater(tiles, bounds, generationOptions, random);
        CarveRoads(tiles, bounds, generationOptions);
        var lots = BuildLots(bounds, generationOptions);
        AssignZones(lots, generationOptions);
        var spawns = BuildSpawnPoints(tiles, bounds);
        var signature = ComputeSignature(options, tiles, lots, spawns);

        return new GeneratedMap
        {
            Options = options,
            Tiles = tiles,
            Lots = lots,
            SpawnPoints = spawns,
            CanonicalSignature = signature
        };
    }

    private static void FillLand(TileFlags[] tiles)
    {
        for (var i = 0; i < tiles.Length; i++)
            tiles[i] = TileFlags.Land;
    }

    private static void CarveWater(TileFlags[] tiles, MapBounds bounds, MapGenerationOptions options, Random random)
    {
        if (options.WaterFraction <= 0f)
            return;

        var waterTiles = (int)(bounds.Area * options.WaterFraction);
        if (waterTiles <= 0)
            return;

        var placed = 0;
        var attempts = 0;
        while (placed < waterTiles && attempts++ < waterTiles * 20)
        {
            var x = random.Next(bounds.MinX, bounds.MaxX + 1);
            var y = random.Next(bounds.MinY, bounds.MaxY + 1);
            if (Math.Abs(x) < bounds.Width / 8 && Math.Abs(y) < bounds.Height / 8)
                continue;

            var index = IndexOf(bounds, new TileCoord(x, y));
            if (tiles[index] == TileFlags.Land)
            {
                tiles[index] = TileFlags.Water;
                placed++;
            }
        }
    }

    private static void CarveRoads(TileFlags[] tiles, MapBounds bounds, MapGenerationOptions options)
    {
        var spacing = Math.Max(4, options.RoadSpacing);
        for (var x = bounds.MinX; x <= bounds.MaxX; x++)
        {
            if (Mod(x, spacing) != 0)
                continue;
            for (var y = bounds.MinY; y <= bounds.MaxY; y++)
            {
                var index = IndexOf(bounds, new TileCoord(x, y));
                if ((tiles[index] & TileFlags.Water) == 0)
                    tiles[index] |= TileFlags.Road;
            }
        }

        for (var y = bounds.MinY; y <= bounds.MaxY; y++)
        {
            if (Mod(y, spacing) != 0)
                continue;
            for (var x = bounds.MinX; x <= bounds.MaxX; x++)
            {
                var index = IndexOf(bounds, new TileCoord(x, y));
                if ((tiles[index] & TileFlags.Water) == 0)
                    tiles[index] |= TileFlags.Road;
            }
        }
    }

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

    private static List<TileCoord> BuildSpawnPoints(TileFlags[] tiles, MapBounds bounds)
    {
        var spawns = new List<TileCoord>();
        var center = new TileCoord(0, 0);
        var centerIndex = IndexOf(bounds, center);
        if (IsWalkable(tiles[centerIndex]))
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
                    if (IsWalkable(tiles[IndexOf(bounds, tile)]) && !spawns.Contains(tile))
                        spawns.Add(tile);
                }
            }
        }

        return spawns;
    }

    private static string ComputeSignature(
        MapOptions options,
        TileFlags[] tiles,
        List<MapLot> lots,
        List<TileCoord> spawns)
    {
        var builder = new StringBuilder();
        builder.Append(options.MapId).Append('|')
            .Append(options.Version).Append('|')
            .Append(options.Seed).Append('|')
            .Append(options.Width).Append('|')
            .Append(options.Height).Append('|');

        foreach (var tile in tiles)
            builder.Append((byte)tile).Append(',');

        foreach (var lot in lots)
        {
            builder.Append(lot.Id.Value).Append(':')
                .Append(lot.Bounds.MinX).Append(',').Append(lot.Bounds.MinY).Append(',')
                .Append(lot.Bounds.MaxX).Append(',').Append(lot.Bounds.MaxY).Append(':')
                .Append(lot.Zone).Append(';');
        }

        foreach (var spawn in spawns)
            builder.Append(spawn.X).Append(',').Append(spawn.Y).Append(';');

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static bool IsWalkable(TileFlags flags) =>
        (flags & (TileFlags.Water | TileFlags.Occupied | TileFlags.Reserved)) == 0 &&
        (flags & (TileFlags.Land | TileFlags.Road)) != 0;

    private static int IndexOf(MapBounds bounds, TileCoord tile) =>
        (tile.Y - bounds.MinY) * bounds.Width + (tile.X - bounds.MinX);

    private static int Mod(int value, int modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}

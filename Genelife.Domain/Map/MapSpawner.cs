using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Components.Map;

namespace Genelife.Map;

public static class MapSpawner
{
    public static TileCoord FindSpawn(MapContext map, ulong sequence)
    {
        var candidates = map.Generated.SpawnPoints;
        if (candidates.Count > 0)
        {
            var nonNegative = candidates.Where(tile => tile.X >= 0 && tile.Y >= 0).ToList();
            var ordered = nonNegative.Count > 0 ? nonNegative : candidates;
            var start = (int)(sequence % (ulong)ordered.Count);
            for (var i = 0; i < ordered.Count; i++)
            {
                var tile = ordered[(start + i) % ordered.Count];
                if (map.Map.IsWalkable(tile))
                    return tile;
            }
        }

        if (map.Map.IsWalkable(TileCoord.Zero))
            return TileCoord.Zero;

        var bounds = map.Options.Bounds;
        var center = bounds.Clamp(TileCoord.Zero);
        var maxRadius = Math.Max(bounds.Width, bounds.Height);
        for (var radius = 1; radius <= maxRadius; radius++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                for (var x = -radius; x <= radius; x++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(y)) != radius)
                        continue;
                    var tile = center.Offset(x, y);
                    if (bounds.Contains(tile) && map.Map.IsWalkable(tile))
                        return tile;
                }
            }
        }

        return center;
    }

    public static void PlaceGenos(World world, Entity genos, MapContext map, ulong sequence)
    {
        var tile = FindSpawn(map, sequence);
        if (world.Has<TilePosition>(genos))
            world.Get<TilePosition>(genos) = new TilePosition(tile);
        else
            world.Add(genos, new TilePosition(tile));

        var (x, y) = map.Options.ToWorld(tile);
        if (world.Has<Location>(genos))
            world.Get<Location>(genos) = new Location(x, y);
        else
            world.Add(genos, new Location(x, y));
    }

    public static void PlaceGenosAt(World world, Entity genos, MapContext map, TileCoord tile)
    {
        if (world.Has<TilePosition>(genos))
            world.Get<TilePosition>(genos) = new TilePosition(tile);
        else
            world.Add(genos, new TilePosition(tile));

        var (x, y) = map.Options.ToWorld(tile);
        if (world.Has<Location>(genos))
            world.Get<Location>(genos) = new Location(x, y);
        else
            world.Add(genos, new Location(x, y));
    }
}

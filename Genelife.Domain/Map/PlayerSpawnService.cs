using Arch.Core;
using Genelife.Components.Housing;
using Genelife.Components.Map;

namespace Genelife.Map;

public static class PlayerSpawnService
{
    public static TileCoord ResolveSpawn(
        World world,
        Entity genos,
        HousingRegistry housing,
        MapContext map,
        ulong sequence)
    {
        if (housing.TryGetUnitForGenos(genos, out var unit) &&
            world.IsAlive(unit) &&
            world.Has<HousingUnit>(unit))
        {
            var property = world.Get<HousingUnit>(unit).Property;
            if (world.IsAlive(property) && world.Has<Placement>(property))
            {
                var entry = world.Get<Placement>(property).Entry;
                if (map.Map.IsWalkable(entry))
                    return entry;
            }
        }

        return MapSpawner.FindSpawn(map, sequence);
    }

    public static TileCoord PlaceAt(
        World world,
        Entity genos,
        HousingRegistry housing,
        MapContext map,
        ulong sequence)
    {
        var tile = ResolveSpawn(world, genos, housing, map, sequence);
        MapSpawner.PlaceGenosAt(world, genos, map, tile);
        return tile;
    }
}

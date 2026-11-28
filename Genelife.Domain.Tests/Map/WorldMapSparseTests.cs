using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Map;

namespace Genelife.UnitTests.Map;

/// <summary>
/// Phase 2 of the 32000x32000 world migration
/// (<c>docs/research/world-scale-migration-32000.md</c> §5.2): WorldMap's
/// occupancy and lot reservations are sparse, and terrain is served by the lazy
/// <see cref="TileGrid"/> — so loading a world never materialises an
/// <c>O(area)</c> tile array.
/// </summary>
public class WorldMapSparseTests
{
    private static GeneratedMap LargeLazyMap(MapOptions options)
    {
        var bounds = options.Bounds;
        var grid = TileGrid.Lazy(bounds, tile =>
            tile.X % 12 == 0 || tile.Y % 12 == 0
                ? TileFlags.Land | TileFlags.Road
                : TileFlags.Land);

        return new GeneratedMap
        {
            Options = options,
            Tiles = grid,
            Lots = new List<MapLot>(),
            SpawnPoints = new List<TileCoord>(),
            CanonicalSignature = "test"
        };
    }

    [Fact]
    public void Large_world_loads_without_materialising_the_dense_tile_array()
    {
        // 32000x32000 = 1.024e9 tiles. The pre-Phase-2 Load cloned layout.Tiles
        // into a TileFlags[area] array (~1 GB) and scanned it for reservations;
        // the sparse map keeps only a reference to the lazy grid.
        var options = new MapOptions { Width = 32000, Height = 32000 };
        var map = new WorldMap();

        map.Load(LargeLazyMap(options), 1);

        Assert.Equal(options, map.Options);
        Assert.Equal(0, map.ReservedTileCount);

        // (15001,15001) is off both road lines, so it is plain land, far outside
        // the old +/-1000 AOI cap.
        var far = new TileCoord(15001, 15001);
        Assert.Equal(TileFlags.Land, map.FlagsAt(far));
        Assert.True(map.IsWalkable(far));

        // Terrain in a 32000-wide world reaches +/-16000; nothing beyond is in bounds.
        Assert.False(map.IsInBounds(new TileCoord(20000, 0)));
    }

    [Fact]
    public void Occupancy_and_reservation_overlay_the_lazy_terrain()
    {
        var options = new MapOptions { Width = 32000, Height = 32000 };
        var map = new WorldMap();
        map.Load(LargeLazyMap(options), 1);

        var anchor = new TileCoord(100, 100);
        map.Place(
            new NetworkId(1),
            BuildingKind.Office,
            new Footprint(2, 2),
            anchor,
            new TileCoord(100, 102));

        Assert.Equal(4, map.OccupiedTileCount);
        Assert.Equal(TileFlags.Land | TileFlags.Occupied, map.FlagsAt(anchor));
        Assert.False(map.IsWalkable(anchor));

        // A road tile keeps its road flag underneath occupancy.
        var onRoad = new TileCoord(108, 5);
        map.Place(
            new NetworkId(2),
            BuildingKind.Office,
            new Footprint(1, 1),
            onRoad,
            new TileCoord(109, 5));
        Assert.Equal(5, map.OccupiedTileCount);
        Assert.Equal(TileFlags.Land | TileFlags.Road | TileFlags.Occupied, map.FlagsAt(onRoad));

        map.Remove(new NetworkId(1));
        Assert.Equal(1, map.OccupiedTileCount);
        Assert.Equal(TileFlags.Land, map.FlagsAt(anchor));
        Assert.True(map.IsWalkable(anchor));

        map.Remove(new NetworkId(2));
        Assert.Equal(0, map.OccupiedTileCount);
    }

    [Fact]
    public void Rebuild_clears_sparse_overlays_in_bounds_proportional_work()
    {
        using var world = World.Create();
        var options = new MapOptions { Width = 32000, Height = 32000 };
        var map = new WorldMap();
        map.Load(LargeLazyMap(options), 1);

        map.Place(
            new NetworkId(1),
            BuildingKind.Office,
            new Footprint(2, 2),
            new TileCoord(50, 50),
            new TileCoord(50, 52));

        map.Rebuild(world);

        Assert.Equal(0, map.OccupiedTileCount);
        Assert.Equal(0, map.ReservedTileCount);
        Assert.Equal(TileFlags.Land, map.FlagsAt(new TileCoord(50, 50)));
    }
}

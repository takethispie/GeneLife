using Arch.Core;
using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Genelife.Api.Snapshots;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Map;
using Genelife.Enums;
using Genelife.Housing;
using Genelife.Map;
using Genelife.Values;

namespace Genelife.UnitTests.Map;

public class MapFoundationTests
{
    private static MapContext SmallMap(World world, int seed = 7) =>
        MapContext.Generate(
            world,
            new MapOptions { Seed = seed, Width = 64, Height = 64 },
            new MapGenerationOptions { RoadSpacing = 10, WaterFraction = 0.01f });

    [Fact]
    public void Generator_is_deterministic_for_the_same_seed()
    {
        var generator = new MapGenerator();
        var options = new MapOptions { Seed = 42, Width = 64, Height = 64 };
        var first = generator.Generate(options);
        var second = generator.Generate(options);

        Assert.Equal(first.CanonicalSignature, second.CanonicalSignature);
        Assert.Equal(first.Lots.Count, second.Lots.Count);
        Assert.Equal(first.SpawnPoints, second.SpawnPoints);
    }

    [Fact]
    public void Generator_varies_with_the_seed()
    {
        var generator = new MapGenerator();
        var first = generator.Generate(new MapOptions { Seed = 1, Width = 64, Height = 64 });
        var second = generator.Generate(new MapOptions { Seed = 2, Width = 64, Height = 64 });

        Assert.NotEqual(first.CanonicalSignature, second.CanonicalSignature);
    }

    [Fact]
    public void Placement_reserves_footprint_and_rejects_overlap()
    {
        using var world = World.Create();
        var map = SmallMap(world);
        var property = world.Create(
            new Property("Test", "Central", Entity.Null, 10, 0.8f, 0.5f, Money.FromUnits(10m)),
            new NetworkId(10));

        var result = map.Placement.TryPlace(
            property,
            new NetworkId(10),
            BuildingKind.Residential,
            new Footprint(4, 3),
            "Central");

        Assert.True(result.Success, result.Message);
        Assert.True(map.Map.TryGetPlacement(new NetworkId(10), out var placed));
        Assert.Equal(12, placed.Tiles().Count());

        var second = world.Create(
            new Property("Test 2", "Central", Entity.Null, 10, 0.8f, 0.5f, Money.FromUnits(10m)),
            new NetworkId(11));
        var overlap = map.Placement.TryPlace(
            second,
            new NetworkId(11),
            BuildingKind.Residential,
            new Footprint(4, 3),
            "Central",
            desiredAnchor: placed.Anchor);

        Assert.False(overlap.Success);
        Assert.Equal(PlacementError.Occupied, overlap.Error);
    }

    [Fact]
    public void Pathfinder_routes_around_a_building()
    {
        using var world = World.Create();
        var map = SmallMap(world);
        map.Map.Place(
            new NetworkId(99),
            BuildingKind.Office,
            new Footprint(1, 1),
            new TileCoord(3, 0),
            new TileCoord(3, 1));

        var path = TilePathfinder.FindPath(map.Map, new TileCoord(0, 0), new TileCoord(6, 0));

        Assert.NotNull(path);
        Assert.DoesNotContain(new TileCoord(3, 0), path);
        Assert.Equal(new TileCoord(6, 0), path![^1]);
    }

    [Fact]
    public void Aoi_keeps_the_players_own_genos_outside_the_viewport()
    {
        var snapshot = new WorldSnapshot(
            10,
            2,
            new List<GenosStatus>
            {
                new() { NetworkId = 1, TileX = 0, TileY = 0 },
                new() { NetworkId = 2, TileX = 100, TileY = 100 }
            },
            new List<CompanyStatus>());

        var filtered = AoiFilter.FilterSnapshotByViewport(snapshot, new Viewport(0, 0, 5), alwaysIncludeGenos: 2);

        Assert.Equal(2, filtered.Genoses.Count);
        Assert.Contains(filtered.Genoses, genos => genos.NetworkId == 2);
    }

    [Fact]
    public void Bootstrap_creates_placements_and_restore_preserves_them()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        var world = engine.World!;
        var map = engine.MapContext!;

        HousingBootstrap.CreateStock(world, engine.Economy!, 0, map: map);
        var state = engine.CaptureState(engine.NetworkIds);

        Assert.NotEmpty(state.Placements);
        Assert.NotEmpty(state.Map.Tiles);

        using var restored = new SimulationEngine();
        restored.Restore(state, restored.NetworkIds);

        Assert.Equal(state.Placements.Count, restored.MapContext!.Map.Placements.Count);
        Assert.Equal(state.Map.Version, restored.MapContext.Map.Version);
    }
}

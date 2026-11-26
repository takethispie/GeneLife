using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Components.Map;
using Genelife.Map;
using Genelife.Application.Systems;

namespace Genelife.UnitTests.Map;

public class MovementTests
{
    [Fact]
    public void Pathfinding_and_movement_walk_a_genos_to_the_target()
    {
        using var world = World.Create();
        var map = MapContext.Generate(
            world,
            new MapOptions { Seed = 3, Width = 64, Height = 64 },
            new MapGenerationOptions { RoadSpacing = 10, WaterFraction = 0f });
        var index = new MovementIndex();
        var genos = world.Create(
            new TilePosition(new TileCoord(0, 0)),
            new MoveTarget(new TileCoord(6, 0)),
            new MovementState(4f),
            new Location(0f, 0f),
            new NetworkId(1));

        index.RequestPath(genos, new TileCoord(0, 0), new TileCoord(6, 0), map.Version);
        var pathfinding = new PathfindingSystem(world, map.Map, index);
        var movement = new MovementSystem(world, map.Map, index, 4f);

        pathfinding.Update();
        for (var i = 0; i < 20; i++)
            movement.Update(0.25f);

        Assert.Equal(new TileCoord(6, 0), world.Get<TilePosition>(genos).Tile);
        Assert.False(world.Get<MoveTarget>(genos).HasTarget);
    }

    [Fact]
    public void Spawner_chooses_a_walkable_tile()
    {
        using var world = World.Create();
        var map = MapContext.Generate(
            world,
            new MapOptions { Seed = 5, Width = 64, Height = 64 },
            new MapGenerationOptions { RoadSpacing = 10, WaterFraction = 0f });
        var genos = world.Create(new Location(0f, 0f), new NetworkId(7));

        MapSpawner.PlaceGenos(world, genos, map, 7);

        Assert.True(map.Map.IsWalkable(world.Get<TilePosition>(genos).Tile));
    }
}

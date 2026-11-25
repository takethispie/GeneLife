using Genelife.Api.Persistence;
using Genelife.Api.Repositories;
using Genelife.Api.Services;
using Genelife.Components.Gen;
using Genelife.Components.Map;
using Genelife.Housing;
using Genelife.Map;

namespace Genelife.UnitTests.Map;

public class MapMigrationTests
{
    [Fact]
    public void V4_snapshot_backfills_map_and_walkable_genos_positions()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        var world = engine.World!;
        HousingBootstrap.CreateStock(world, engine.Economy!, 0, map: engine.MapContext!);
        var genosRepo = new GenosRepository(world, engine.NetworkIds, engine.Economy, engine.MapContext);
        genosRepo.Add("Legacy", 30, "legacy-player");

        var state = engine.CaptureState(engine.NetworkIds);
        state.SchemaVersion = 4;
        state.Map = new PersistedMap();
        state.Placements.Clear();
        foreach (var genos in state.Genoses)
        {
            genos.HasTilePosition = false;
            genos.TileX = 0;
            genos.TileY = 0;
            genos.X = 0f;
            genos.Y = 0f;
        }

        SnapshotMigrations.Upgrade(state, 4);

        using var restored = new SimulationEngine();
        restored.Restore(state, restored.NetworkIds);

        var map = restored.MapContext!;
        Assert.NotEmpty(map.Map.Placements);

        var genosQuery = new Arch.Core.QueryDescription().WithAll<TilePosition>();
        var allWalkable = true;
        restored.World!.Query(in genosQuery, (ref TilePosition position) =>
        {
            if (!map.Map.IsWalkable(position.Tile))
                allWalkable = false;
        });
        Assert.True(allWalkable);
    }

    [Fact]
    public void Mid_route_movement_state_round_trips()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        var world = engine.World!;
        var genosRepo = new GenosRepository(world, engine.NetworkIds, engine.Economy, engine.MapContext);
        var id = genosRepo.Add("Mover", 30, "player-move");

        Assert.True(engine.NetworkIds.TryGetEntity(id, out var genos));
        world.Get<TilePosition>(genos) = new TilePosition(new TileCoord(2, 0));
        if (!world.Has<MovementState>(genos))
            world.Add(genos, new MovementState(4f));
        world.Get<MovementState>(genos) = new MovementState(4f)
        {
            From = new TileCoord(2, 0),
            To = new TileCoord(3, 0),
            Progress = 0.5f,
            IsMoving = true
        };
        if (!world.Has<MoveTarget>(genos))
            world.Add(genos, new MoveTarget(new TileCoord(6, 0)));

        var state = engine.CaptureState(engine.NetworkIds);
        var persisted = state.Genoses.Single(item => item.NetworkId == id.Value);
        Assert.Equal(2, persisted.TileX);
        Assert.Equal(0.5f, persisted.MoveProgress);

        using var restored = new SimulationEngine();
        restored.Restore(state, restored.NetworkIds);

        Assert.True(restored.NetworkIds.TryGetEntity(id, out var restoredGenos));
        Assert.Equal(new TileCoord(2, 0), restored.World!.Get<TilePosition>(restoredGenos).Tile);
        Assert.True(restored.World.Get<MoveTarget>(restoredGenos).HasTarget);
        Assert.Equal(new TileCoord(6, 0), restored.World.Get<MoveTarget>(restoredGenos).Tile);
        Assert.Equal(0.5f, restored.World.Get<MovementState>(restoredGenos).Progress);
    }
}

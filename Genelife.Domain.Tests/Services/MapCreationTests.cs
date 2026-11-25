using Genelife.Api.Commands;
using Genelife.Api.DTOs;
using Genelife.Api.Persistence;
using Genelife.Api.Services;

namespace Genelife.UnitTests.Services;

/// <summary>
/// Covers the destructive world reset orchestration: a new map, a fresh
/// population, recreated player sims and replication-compatible ticking.
/// </summary>
public class MapCreationTests
{
    private static CreateMapRequest Request(
        int seed = 2,
        int population = 0,
        bool confirm = true,
        int? adults = null,
        int? children = null) =>
        new(seed, 128, 128, population, seed, adults, children, confirm);

    [Fact]
    public async Task CreateMap_replaces_the_layout_and_bumps_the_map_version()
    {
        using var manager = new SimulationManager(new SimulationEngine(), new InMemorySnapshotStore());
        manager.StartSimulation();

        var before = manager.GetMapStatus();
        var result = await manager.CreateMap(Request(seed: 9), Array.Empty<string>(), "test");

        Assert.True(manager.HasMap);
        Assert.True(manager.IsRunning);
        Assert.Equal(9, result.Seed);
        Assert.Equal(9, manager.GetMapStatus().Seed);
        Assert.True(result.Version > before.Version);
        Assert.True(result.LayoutVersion >= 1);
        Assert.Equal(128, result.Width);
        Assert.Equal(128, result.Height);
    }

    [Fact]
    public async Task CreateMap_starts_the_new_world_at_the_next_tick()
    {
        var engine = new SimulationEngine();
        using var manager = new SimulationManager(engine, new InMemorySnapshotStore());
        manager.StartSimulation();

        var previousTick = engine.CurrentTick;
        await manager.CreateMap(Request(seed: 3), Array.Empty<string>(), "test");

        Assert.True(engine.CurrentTick > previousTick);
    }

    [Fact]
    public async Task CreateMap_generates_buildings_and_population()
    {
        using var manager = new SimulationManager(new SimulationEngine(), new InMemorySnapshotStore());
        manager.StartSimulation();

        var result = await manager.CreateMap(Request(seed: 4, population: 40), Array.Empty<string>(), "test");

        Assert.True(result.Buildings > 0);
        Assert.True(result.Population > 0);
        Assert.Equal(0, result.Players);
    }

    [Fact]
    public async Task CreateMap_recreates_player_characters()
    {
        var engine = new SimulationEngine();
        using var manager = new SimulationManager(engine, new InMemorySnapshotStore());
        manager.StartSimulation();

        var result = await manager.CreateMap(
            Request(seed: 5, population: 20),
            new[] { "alice", "bob" },
            "test");

        Assert.Equal(2, result.Players);
        Assert.NotEqual(0ul, engine.GetPlayerSimNetworkId("alice"));
        Assert.NotEqual(0ul, engine.GetPlayerSimNetworkId("bob"));
    }

    [Fact]
    public async Task CreateMap_requires_confirmation_when_a_map_exists()
    {
        using var manager = new SimulationManager(new SimulationEngine(), new InMemorySnapshotStore());
        manager.StartSimulation();
        var before = manager.GetMapStatus();

        await Assert.ThrowsAsync<WorldResetConfirmationRequiredException>(() =>
            manager.CreateMap(Request(confirm: false, seed: 15), Array.Empty<string>(), "test"));

        var after = manager.GetMapStatus();
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(before.Seed, after.Seed);
    }

    [Fact]
    public async Task CreateMap_with_zero_population_leaves_a_running_empty_map()
    {
        using var manager = new SimulationManager(new SimulationEngine(), new InMemorySnapshotStore());
        manager.StartSimulation();

        var result = await manager.CreateMap(Request(seed: 6, population: 0), Array.Empty<string>(), "test");

        Assert.Equal(0, result.Population);
        Assert.True(result.Buildings >= 0);
        Assert.True(manager.IsRunning);
        Assert.True(manager.HasMap);
    }

    [Fact]
    public async Task CreateMap_works_while_the_simulation_is_stopped()
    {
        using var manager = new SimulationManager(new SimulationEngine(), new InMemorySnapshotStore());
        manager.StartSimulation();
        manager.StopSimulation();

        var result = await manager.CreateMap(Request(seed: 7), Array.Empty<string>(), "test");

        Assert.True(manager.IsRunning);
        Assert.True(manager.HasMap);
        Assert.Equal(7, result.Seed);
    }

    [Fact]
    public async Task Pending_admin_commands_are_discarded_by_a_reset()
    {
        var engine = new SimulationEngine();
        using var manager = new SimulationManager(engine, new InMemorySnapshotStore());
        manager.StartSimulation();

        var command = new CreateSimCommand(Guid.NewGuid(), "test", "Ghost", 30);
        engine.EnqueueAdminCommand(command);

        await manager.CreateMap(Request(seed: 8), Array.Empty<string>(), "test");
        engine.Tick(1f / 20f);

        Assert.Null(engine.GetCommandResult(command.CommandId));
    }

    private sealed class InMemorySnapshotStore : ISnapshotStore
    {
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

        public void Save(PersistedWorldState state)
        {
        }

        public PersistedWorldState? LoadLatest() => null;
    }
}

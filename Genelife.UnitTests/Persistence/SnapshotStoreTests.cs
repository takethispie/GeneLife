using Arch.Core;
using Genelife.Api.Persistence;
using Genelife.Api.Services;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Enums;
using Microsoft.Data.Sqlite;

namespace Genelife.UnitTests.Persistence;

public class SnapshotStoreTests : IDisposable
{
    private readonly string dbPath;

    public SnapshotStoreTests()
    {
        dbPath = Path.Combine(Path.GetTempPath(), $"test-snapshots-{Guid.NewGuid()}.db");
    }

    private SnapshotStore CreateStore()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new SnapshotStoreOptions
        {
            DatabasePath = dbPath
        });
        return new SnapshotStore(options);
    }

    [Fact]
    public Task Initialize_CreatesTableAndIndex()
    {
        using var store = CreateStore();
        store.InitializeAsync().GetAwaiter().GetResult();

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='snapshots'";
        var tableName = cmd.ExecuteScalar()?.ToString();
        Assert.Equal("snapshots", tableName);

        return Task.CompletedTask;
    }

    [Fact]
    public Task Save_StoresState()
    {
        using var store = CreateStore();
        store.InitializeAsync().GetAwaiter().GetResult();

        var state = new PersistedWorldState
        {
            SchemaVersion = PersistedWorldState.CurrentSchemaVersion,
            Tick = 42,
            Sims =
            [
                new PersistedSim
                {
                    Name = "Alice", Age = 30, NetworkId = 1,
                    Hunger = 80, Energy = 90, Hygiene = 85, Bladder = 75,
                    ActionType = "Idle", TimeRemaining = 0,
                    PlayerId = "player-1"
                }
            ],
            Companies =
            [
                new PersistedCompany { Name = "Acme", NetworkId = 10 }
            ]
        };

        store.Save(state);

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM snapshots";
        var count = (long)cmd.ExecuteScalar()!;
        Assert.Equal(1, count);

        return Task.CompletedTask;
    }

    [Fact]
    public Task LoadLatest_ReturnsSavedState()
    {
        using var store = CreateStore();
        store.InitializeAsync().GetAwaiter().GetResult();

        var state = new PersistedWorldState
        {
            SchemaVersion = PersistedWorldState.CurrentSchemaVersion,
            Tick = 99,
            Sims = [new PersistedSim { Name = "Bob", Age = 25, NetworkId = 5, ActionType = "Sleeping" }],
            Companies = [new PersistedCompany { Name = "GlobalCorp", NetworkId = 20 }]
        };

        store.Save(state);

        var loaded = store.LoadLatest();
        Assert.NotNull(loaded);
        Assert.Equal(99, loaded!.Tick);
        Assert.Equal(PersistedWorldState.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Single(loaded.Sims);
        Assert.Equal("Bob", loaded.Sims[0].Name);
        Assert.Equal(25, loaded.Sims[0].Age);
        Assert.Equal((ulong)5, loaded.Sims[0].NetworkId);
        Assert.Equal("Sleeping", loaded.Sims[0].ActionType);
        Assert.Single(loaded.Companies);
        Assert.Equal("GlobalCorp", loaded.Companies[0].Name);
        Assert.Equal((ulong)20, loaded.Companies[0].NetworkId);

        return Task.CompletedTask;
    }

    [Fact]
    public Task LoadLatest_ReturnsLatestByTick()
    {
        using var store = CreateStore();
        store.InitializeAsync().GetAwaiter().GetResult();

        store.Save(new PersistedWorldState
        {
            SchemaVersion = PersistedWorldState.CurrentSchemaVersion,
            Tick = 10,
            Sims = [new PersistedSim { Name = "Early" }]
        });

        store.Save(new PersistedWorldState
        {
            SchemaVersion = PersistedWorldState.CurrentSchemaVersion,
            Tick = 50,
            Sims = [new PersistedSim { Name = "Late" }]
        });

        store.Save(new PersistedWorldState
        {
            SchemaVersion = PersistedWorldState.CurrentSchemaVersion,
            Tick = 30,
            Sims = [new PersistedSim { Name = "Middle" }]
        });

        var loaded = store.LoadLatest();
        Assert.NotNull(loaded);
        Assert.Equal(50, loaded!.Tick);
        Assert.Equal("Late", loaded.Sims[0].Name);

        return Task.CompletedTask;
    }

    [Fact]
    public Task LoadLatest_ReturnsNullForEmptyDb()
    {
        using var store = CreateStore();
        store.InitializeAsync().GetAwaiter().GetResult();

        var loaded = store.LoadLatest();
        Assert.Null(loaded);

        return Task.CompletedTask;
    }

    [Fact]
    public Task LoadLatest_MigratesV3SnapshotToCurrentSchema()
    {
        using var store = CreateStore();
        store.InitializeAsync().GetAwaiter().GetResult();

        store.Save(new PersistedWorldState
        {
            SchemaVersion = 3,
            Tick = 123,
            HousingMigrated = true,
            Sims =
            [
                new PersistedSim
                {
                    Name = "Legacy", Age = 72, NetworkId = 7,
                    ActionType = "Idle", Stage = string.Empty
                }
            ]
        });

        var loaded = store.LoadLatest();
        Assert.NotNull(loaded);
        Assert.Equal(PersistedWorldState.CurrentSchemaVersion, loaded!.SchemaVersion);
        Assert.False(loaded.LifecycleMigrated);
        Assert.Single(loaded.Sims);
        Assert.Equal(72, loaded.Sims[0].Age);
        return Task.CompletedTask;
    }

    [Fact]
    public Task LoadLatest_ThrowsForNewerSchema()
    {
        using var store = CreateStore();
        store.InitializeAsync().GetAwaiter().GetResult();

        var futureState = new PersistedWorldState
        {
            SchemaVersion = 999,
            Tick = 1,
            Sims = [new PersistedSim { Name = "Future" }]
        };

        store.Save(futureState);

        var ex = Assert.Throws<InvalidOperationException>(() => store.LoadLatest());
        Assert.Contains("newer", ex.Message);

        return Task.CompletedTask;
    }

    [Fact]
    public Task LoadLatest_ThrowsForOlderSchema()
    {
        using var store = CreateStore();
        store.InitializeAsync().GetAwaiter().GetResult();

        var oldState = new PersistedWorldState
        {
            SchemaVersion = 0,
            Tick = 1,
            Sims = [new PersistedSim { Name = "Old" }]
        };

        store.Save(oldState);

        var ex = Assert.Throws<InvalidOperationException>(() => store.LoadLatest());
        Assert.Contains("older", ex.Message);

        return Task.CompletedTask;
    }

    [Fact]
    public Task Save_IsAppendOnly()
    {
        using var store = CreateStore();
        store.InitializeAsync().GetAwaiter().GetResult();

        for (int i = 0; i < 5; i++)
        {
            store.Save(new PersistedWorldState
            {
                SchemaVersion = PersistedWorldState.CurrentSchemaVersion,
                Tick = i,
                Sims = [new PersistedSim { Name = $"Sim_{i}" }]
            });
        }

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM snapshots";
        var count = (long)cmd.ExecuteScalar()!;
        Assert.Equal(5, count);

        return Task.CompletedTask;
    }

    [Fact]
    public Task CaptureState_RoundTripsSimComponents()
    {
        var registry = new NetworkIdRegistry();
        using var engine = new SimulationEngine();
        engine.Initialize();
        try
        {
            engine.ExecuteWithWorld(world =>
            {
                var entity = world.Create(
                    new GenName("TestSim"),
                    new Needs { Hunger = 70, Energy = 60, Hygiene = 50, Bladder = 40 },
                    new CurrentAction(ActionType.Eating, 3.5f),
                    new Alive(30)
                );
                var nid = new NetworkId(1);
                world.Add(entity, nid);
                registry.AllocateWithValue(entity, nid);
                return true;
            });

            var state = engine.CaptureState(registry);
            Assert.Single(state.Sims);
            var sim = state.Sims[0];
            Assert.Equal("TestSim", sim.Name);
            Assert.Equal(30, sim.Age);
            Assert.Equal(70, sim.Hunger);
            Assert.Equal(60, sim.Energy);
            Assert.Equal(50, sim.Hygiene);
            Assert.Equal(40, sim.Bladder);
            Assert.Equal("Eating", sim.ActionType);
            Assert.Equal(3.5f, sim.TimeRemaining);
            Assert.Equal((ulong)1, sim.NetworkId);
            Assert.False(sim.IsLookingForWork);
            Assert.Null(sim.PlayerId);
        }
        finally { engine.Shutdown(); }
        return Task.CompletedTask;
    }

    [Fact]
    public Task CaptureState_RoundTripsCompanyComponents()
    {
        var registry = new NetworkIdRegistry();
        using var engine = new SimulationEngine();
        engine.Initialize();
        try
        {
            engine.ExecuteWithWorld(world =>
            {
                var entity = world.Create(new CompanyName("TestCorp"));
                var nid = new NetworkId(5);
                world.Add(entity, nid);
                registry.AllocateWithValue(entity, nid);
                return true;
            });

            var state = engine.CaptureState(registry);
            Assert.Single(state.Companies);
            var company = state.Companies[0];
            Assert.Equal("TestCorp", company.Name);
            Assert.Equal((ulong)5, company.NetworkId);
        }
        finally { engine.Shutdown(); }
        return Task.CompletedTask;
    }

    [Fact]
    public Task CaptureState_IncludesPlayerControlled()
    {
        var registry = new NetworkIdRegistry();
        using var engine = new SimulationEngine();
        engine.Initialize();
        try
        {
            engine.ExecuteWithWorld(world =>
            {
                var entity = world.Create(
                    new GenName("Player"),
                    new Needs(),
                    new CurrentAction(ActionType.Idle, 0),
                    new Alive(25),
                    new PlayerControlled("p42")
                );
                var nid = new NetworkId(1);
                world.Add(entity, nid);
                registry.AllocateWithValue(entity, nid);
                return true;
            });

            var state = engine.CaptureState(registry);
            Assert.Single(state.Sims);
            Assert.Equal("p42", state.Sims[0].PlayerId);
        }
        finally { engine.Shutdown(); }
        return Task.CompletedTask;
    }

    [Fact]
    public Task CaptureState_IncludesJobSeeker()
    {
        var registry = new NetworkIdRegistry();
        using var engine = new SimulationEngine();
        engine.Initialize();
        try
        {
            engine.ExecuteWithWorld(world =>
            {
                var entity = world.Create(
                    new GenName("JobSeeker"),
                    new Needs(),
                    new CurrentAction(ActionType.Idle, 0),
                    new Alive(22),
                    new JobSeeker(true)
                );
                var nid = new NetworkId(1);
                world.Add(entity, nid);
                registry.AllocateWithValue(entity, nid);
                return true;
            });

            var state = engine.CaptureState(registry);
            Assert.Single(state.Sims);
            Assert.True(state.Sims[0].IsLookingForWork);
        }
        finally { engine.Shutdown(); }
        return Task.CompletedTask;
    }

    [Fact]
    public Task Restore_RecreatesSimEntities()
    {
        var registry = new NetworkIdRegistry();
        using var engine = new SimulationEngine();

        var state = new PersistedWorldState
        {
            SchemaVersion = PersistedWorldState.CurrentSchemaVersion,
            Tick = 42,
            Sims =
            [
                new PersistedSim
                {
                    Name = "Alice", Age = 30, NetworkId = 1,
                    Hunger = 80, Energy = 90, Hygiene = 85, Bladder = 75,
                    ActionType = "Sleeping", TimeRemaining = 4.2f,
                    PlayerId = "p1"
                }
            ],
            Companies =
            [
                new PersistedCompany { Name = "Acme", NetworkId = 10 }
            ]
        };

        engine.Restore(state, registry);
        try
        {
            Assert.True(engine.IsRunning);

            var sim = engine.ExecuteWithWorld(world =>
            {
                var query = new Arch.Core.QueryDescription().WithAll<GenName>();
                var result = new List<(string Name, float TimeRemaining, string ActionType)>();
                world.Query(in query, (ref GenName name, ref CurrentAction action) =>
                {
                    result.Add((name.Name, action.TimeRemaining, action.Type.ToString()));
                });
                return result;
            });

            Assert.Single(sim);
            Assert.Equal("Alice", sim[0].Name);
            Assert.Equal("Sleeping", sim[0].ActionType);
            Assert.Equal(4.2f, sim[0].TimeRemaining);

            engine.Tick(1f / 20f);
            var snapshot = engine.GetLatestSnapshot();
            Assert.NotNull(snapshot);
            Assert.Equal(43, snapshot!.Tick);

            var snapSim = snapshot.Sims.Find(s => s.Name == "Alice");
            Assert.NotNull(snapSim);

            var company = snapshot.Companies.Find(c => c.Name == "Acme");
            Assert.NotNull(company);
            Assert.Equal("Acme", company.Name);
        }
        finally { engine.Shutdown(); }
        return Task.CompletedTask;
    }

    [Fact]
    public Task Restore_PreservesTickCount()
    {
        var registry = new NetworkIdRegistry();
        using var engine = new SimulationEngine();

        var state = new PersistedWorldState
        {
            SchemaVersion = PersistedWorldState.CurrentSchemaVersion,
            Tick = 999,
            Sims = []
        };

        engine.Restore(state, registry);
        try
        {
            engine.Tick(1f / 20f);
            var snapshot = engine.GetLatestSnapshot();
            Assert.NotNull(snapshot);
            Assert.True(snapshot!.Tick > 999, $"Expected tick > 999, got {snapshot.Tick}");
        }
        finally { engine.Shutdown(); }
        return Task.CompletedTask;
    }

    [Fact]
    public Task Restore_SetsNetworkIdsCorrectly()
    {
        var registry = new NetworkIdRegistry();
        using var engine = new SimulationEngine();

        var state = new PersistedWorldState
        {
            SchemaVersion = PersistedWorldState.CurrentSchemaVersion,
            Tick = 0,
            Sims =
            [
                new PersistedSim { Name = "S1", NetworkId = 5 },
                new PersistedSim { Name = "S2", NetworkId = 10 }
            ]
        };

        engine.Restore(state, registry);
        try
        {
            Assert.True(registry.TryGetEntity(new NetworkId(5), out _));
            Assert.True(registry.TryGetEntity(new NetworkId(10), out _));

            var newEntity = engine.ExecuteWithWorld(world =>
            {
                var e = world.Create(new GenName("New"));
                var nid = registry.Allocate(e);
                world.Add(e, nid);
                return nid;
            });
            Assert.True(newEntity.Value > 10);
        }
        finally { engine.Shutdown(); }
        return Task.CompletedTask;
    }

    [Fact]
    public Task Restore_ThrowsForNonMatchingSchema()
    {
        var registry = new NetworkIdRegistry();
        using var engine = new SimulationEngine();

        var state = new PersistedWorldState
        {
            SchemaVersion = 999,
            Tick = 0
        };

        var ex = Assert.Throws<InvalidOperationException>(() => engine.Restore(state, registry));
        Assert.Contains("schema", ex.Message);
        Assert.False(engine.IsRunning);

        return Task.CompletedTask;
    }

    [Fact]
    public Task Restore_RebuildsSnapshotInSingleTick()
    {
        var registry = new NetworkIdRegistry();
        using var engine = new SimulationEngine();

        var state = new PersistedWorldState
        {
            SchemaVersion = PersistedWorldState.CurrentSchemaVersion,
            Tick = 100,
            Sims =
            [
                new PersistedSim { Name = "A", NetworkId = 1, ActionType = "Idle" },
                new PersistedSim { Name = "B", NetworkId = 2, ActionType = "Idle" }
            ],
            Companies =
            [
                new PersistedCompany { Name = "C", NetworkId = 10 }
            ]
        };

        engine.Restore(state, registry);
        try
        {
            engine.Tick(1f / 20f);

            var snapshot = engine.GetLatestSnapshot();
            Assert.NotNull(snapshot);
            Assert.True(snapshot!.Tick >= 101, $"Expected tick >= 101, got {snapshot.Tick}");
            Assert.True(snapshot.Sims.Count >= 2, $"Expected at least 2 sims in snapshot, got {snapshot.Sims.Count}");
        }
        finally { engine.Shutdown(); }
        return Task.CompletedTask;
    }

    [Fact]
    public Task SaveSnapshot_WritesAppendOnlyStore()
    {
        using var store = CreateStore();
        store.InitializeAsync().GetAwaiter().GetResult();

        var registry = new NetworkIdRegistry();
        using var engine = new SimulationEngine();
        engine.Initialize();
        try
        {
            engine.ExecuteWithWorld(world =>
            {
                var e = world.Create(new GenName("S1"), new Needs(), new CurrentAction(ActionType.Idle, 0), new Alive(20));
                var nid = new NetworkId(1);
                world.Add(e, nid);
                registry.AllocateWithValue(e, nid);
                return true;
            });

            var state1 = engine.CaptureState(registry);
            store.Save(state1);

            engine.Tick(1f / 20f);

            var state2 = engine.CaptureState(registry);
            store.Save(state2);

            var latest = store.LoadLatest();
            Assert.NotNull(latest);
            Assert.True(latest!.Tick > state1.Tick);
        }
        finally { engine.Shutdown(); }
        return Task.CompletedTask;
    }

    [Fact]
    public Task AllocateWithValue_RejectsDuplicateNetworkId()
    {
        var registry = new NetworkIdRegistry();
        var world = World.Create();
        var entity1 = world.Create(new GenName("First"));
        var entity2 = world.Create(new GenName("Second"));

        registry.AllocateWithValue(entity1, new NetworkId(5));

        var ex = Assert.Throws<ArgumentException>(() =>
            registry.AllocateWithValue(entity2, new NetworkId(5)));
        Assert.Contains("already mapped", ex.Message);
        world.Dispose();

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (File.Exists(dbPath))
            File.Delete(dbPath);
    }
}
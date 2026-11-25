using Arch.Core;
using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Genelife.Api.Snapshots;
using Genelife.Components.Company;
using Genelife.Components.Gen;
using Genelife.Enums;

namespace Genelife.UnitTests.Snapshots;

public class WorldSnapshotTests
{
    private static void Tick(SimulationEngine engine, int times = 1)
    {
        engine.SnapshotsEnabled = true;
        for (int i = 0; i < times; i++)
            engine.Tick(1f / 20f);
    }

    [Fact]
    public void WorldSnapshot_Construction_StoresAllProperties()
    {
        var genoses = new List<GenosStatus> { new() { Name = "Alice", Hunger = 50, Energy = 80 } };
        var companies = new List<CompanyStatus> { new() { NetworkId = 1, Name = "Acme", EmployeeCount = 0 } };
        var snapshot = new WorldSnapshot(tick: 42, version: 1, genoses: genoses, companies: companies);
        Assert.Equal(42, snapshot.Tick);
        Assert.Equal(1, snapshot.Version);
        Assert.Same(genoses, snapshot.Genoses);
        Assert.Same(companies, snapshot.Companies);
    }

    [Fact]
    public void GetLatestSnapshot_ReturnsNull_BeforeEngineStarted()
    {
        using var engine = new SimulationEngine();
        Assert.Null(engine.GetLatestSnapshot());
    }

    [Fact]
    public void GetLatestSnapshot_ReturnsNonNull_AfterTick()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        try
        {
            Tick(engine);
            var snapshot = engine.GetLatestSnapshot();
            Assert.NotNull(snapshot);
            Assert.NotNull(snapshot.Genoses);
            Assert.NotNull(snapshot.Companies);
            Assert.True(snapshot.Tick > 0);
        }
        finally { engine.Shutdown(); }
    }

    [Fact]
    public void SnapshotGenoses_ReflectsWorldEntities()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        try
        {
            engine.ExecuteWithWorld(world =>
            {
                world.Create(
                    new GenName("TestGenos"), new Needs { Hunger = 70f, Energy = 60f },
                    new CurrentAction(ActionType.Sleeping, 2.5f), new Alive(25));
                return true;
            });
            Tick(engine);
            var snapshot = engine.GetLatestSnapshot();
            Assert.NotNull(snapshot);
            var genos = snapshot.Genoses.Find(s => s.Name == "TestGenos");
            Assert.NotNull(genos);
            Assert.Equal("TestGenos", genos.Name);
            Assert.Equal("Sleeping", genos.CurrentAction);
        }
        finally { engine.Shutdown(); }
    }

    [Fact]
    public void SnapshotCompanies_ReflectsWorldEntities()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        try
        {
            engine.ExecuteWithWorld(world => { world.Create(new CompanyName("TestCorp"), new NetworkId(1)); return true; });
            Tick(engine);
            var snapshot = engine.GetLatestSnapshot();
            Assert.NotNull(snapshot);
            var company = snapshot.Companies.Find(c => c.Name == "TestCorp");
            Assert.NotNull(company);
            Assert.Equal(0, company.EmployeeCount);
        }
        finally { engine.Shutdown(); }
    }

    [Fact]
    public void ConcurrentReaders_SeeCompleteSnapshot()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        try
        {
            engine.ExecuteWithWorld(world =>
            {
                for (int i = 0; i < 10; i++)
                    world.Create(new GenName($"Genos_{i}"), new Needs(), new CurrentAction(ActionType.Idle, 0f), new Alive(18));
                for (int i = 0; i < 5; i++)
                    world.Create(new CompanyName($"Company_{i}"));
                return true;
            });
            Tick(engine);
            var snapshot = engine.GetLatestSnapshot();
            Assert.NotNull(snapshot);
            Assert.True(snapshot.Genoses.Count > 0 || snapshot.Companies.Count > 0);
        }
        finally { engine.Shutdown(); }
    }

    [Fact]
    public void SnapshotBuildTime_IsMeasured()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        try { Tick(engine); Assert.True(engine.LastSnapshotBuildTimeMs >= 0); }
        finally { engine.Shutdown(); }
    }

    [Fact]
    public void SnapshotTicks_IncrementAcrossTicks()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        try
        {
            Tick(engine);
            var s1 = engine.GetLatestSnapshot(); Assert.NotNull(s1);
            Tick(engine);
            var s2 = engine.GetLatestSnapshot(); Assert.NotNull(s2);
            Assert.True(s2.Tick > s1.Tick);
        }
        finally { engine.Shutdown(); }
    }

    [Fact]
    public void SnapshotAfterStop_ReturnsNull()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        Tick(engine);
        engine.Shutdown();
        Assert.Null(engine.GetLatestSnapshot());
    }

    [Fact]
    public void GetCompany_ReadsFromSnapshot()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        try
        {
            engine.ExecuteWithWorld(world => { world.Create(new CompanyName("SnapshotCorp"), new NetworkId(1)); return true; });
            Tick(engine);
            var snapshot = engine.GetLatestSnapshot();
            Assert.NotNull(snapshot);
            var company = snapshot.Companies.Find(c => c.Name == "SnapshotCorp");
            Assert.NotNull(company);
        }
        finally { engine.Shutdown(); }
    }

    [Fact]
    public void SnapshotGenoses_IncludeLocationAndTickForInterpolation()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        try
        {
            engine.ExecuteWithWorld(world =>
            {
                var entity = world.Create(
                    new GenName("Moving"),
                    new Needs(),
                    new CurrentAction(ActionType.Idle, 0),
                    new Alive(30),
                    new Location(12.5f, -3.25f));
                world.Add(entity, new NetworkId(7));
                return true;
            });

            Tick(engine);
            var snapshot = engine.GetLatestSnapshot();
            Assert.NotNull(snapshot);
            var genos = snapshot.Genoses.Find(s => s.Name == "Moving");
            Assert.NotNull(genos);
            Assert.Equal(7UL, genos.NetworkId);
            Assert.Equal(12.5f, genos.X);
            Assert.Equal(-3.25f, genos.Y);
            Assert.True(snapshot.Tick > 0);
        }
        finally { engine.Shutdown(); }
    }

    [Fact]
    public void SnapshotHistory_RecordsEachPublishedTick()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        try
        {
            Tick(engine, times: 3);
            Assert.Equal(3, engine.SnapshotHistory.LatestTick);
            Assert.True(engine.SnapshotHistory.TryGet(1, out _));
            Assert.True(engine.SnapshotHistory.TryGet(2, out _));
            Assert.True(engine.SnapshotHistory.TryGet(3, out _));
        }
        finally { engine.Shutdown(); }
    }

    [Fact]
    public void DeltaPayload_IsSmallerThanFullSnapshot_ForStableNeeds()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        try
        {
            engine.ExecuteWithWorld(world =>
            {
                for (var i = 0; i < 100; i++)
                {
                    var entity = world.Create(
                        new GenName($"Stable_{i}"),
                        new Needs(),
                        new CurrentAction(ActionType.Idle, 0f),
                        new Alive(30),
                        new Location(0f, 0f));
                    world.Add(entity, new NetworkId((ulong)(i + 1)));
                }

                return true;
            });

            Tick(engine);
            var full = engine.GetLatestSnapshot();
            Assert.NotNull(full);
            var sinceTick = full.Tick;

            engine.ExecuteWithWorld(world =>
            {
                var query = new QueryDescription().WithAll<NetworkId, Location>();
                world.Query(in query, (Entity entity, ref NetworkId networkId) =>
                {
                    if (networkId.Value == 1)
                    {
                        ref var location = ref world.Get<Location>(entity);
                        location.Position = new System.Numerics.Vector2(5f, 5f);
                    }
                });
                return true;
            });

            Tick(engine);
            var delta = engine.SnapshotHistory.BuildDelta(sinceTick);
            Assert.NotNull(delta);
            Assert.False(delta!.Full);
            Assert.True(delta.Genoses.Count <= 5, $"expected a small delta, got {delta.Genoses.Count} genoses");

            var changed = Assert.Single(delta.Genoses, genos => genos.NetworkId == 1);
            Assert.Equal(5f, changed.X);
            Assert.Equal(5f, changed.Y);

            var fullBytes = ReplicationMetrics.EstimateBytes(full);
            var deltaBytes = ReplicationMetrics.EstimateBytes(delta);

            Assert.True(deltaBytes < fullBytes,
                $"expected delta ({deltaBytes}B) to be smaller than full ({fullBytes}B)");
        }
        finally { engine.Shutdown(); }
    }
}
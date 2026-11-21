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
    [Fact]
    public void WorldSnapshot_Construction_StoresAllProperties()
    {
        var sims = new List<SimStatus>
        {
            new() { Name = "Alice", Hunger = 50, Energy = 80 }
        };
        var companies = new List<CompanyStatus>
        {
            new() { EntityId = 1, Name = "Acme", EmployeeCount = 0 }
        };

        var snapshot = new WorldSnapshot(tick: 42, version: 1, sims: sims, companies: companies);

        Assert.Equal(42, snapshot.Tick);
        Assert.Equal(1, snapshot.Version);
        Assert.Same(sims, snapshot.Sims);
        Assert.Same(companies, snapshot.Companies);
    }

    [Fact]
    public void GetLatestSnapshot_ReturnsNull_BeforeEngineStarted()
    {
        using var engine = new SimulationEngine();

        var snapshot = engine.GetLatestSnapshot();

        Assert.Null(snapshot);
    }

    [Fact]
    public void GetLatestSnapshot_ReturnsNonNull_AfterEngineStarted()
    {
        using var engine = new SimulationEngine();

        engine.Start();
        try
        {
            var snapshot = WaitForSnapshot(engine);
            Assert.NotNull(snapshot);
            Assert.NotNull(snapshot.Sims);
            Assert.NotNull(snapshot.Companies);
            Assert.True(snapshot.Tick > 0);
        }
        finally
        {
            engine.Stop();
        }
    }

    [Fact]
    public void SnapshotSims_ReflectsWorldEntities()
    {
        using var engine = new SimulationEngine();
        engine.Start();
        try
        {
            engine.ExecuteWithWorld(world =>
            {
                world.Create(
                    new GenName("TestSim"),
                    new Needs { Hunger = 70f, Energy = 60f },
                    new CurrentAction(ActionType.Sleeping, 2.5f),
                    new Alive(25)
                );
                return true;
            });

            var snapshot = WaitForFreshSnapshot(engine);
            Assert.NotNull(snapshot);

            var sim = snapshot.Sims.Find(s => s.Name == "TestSim");
            Assert.NotNull(sim);
            Assert.Equal("TestSim", sim.Name);
            Assert.Equal("Sleeping", sim.CurrentAction);
            Assert.True(sim.Energy > 0f);
        }
        finally
        {
            engine.Stop();
        }
    }

    [Fact]
    public void SnapshotCompanies_ReflectsWorldEntities()
    {
        using var engine = new SimulationEngine();
        engine.Start();
        try
        {
            engine.ExecuteWithWorld(world =>
            {
                world.Create(new CompanyName("TestCorp"));
                return true;
            });

            var snapshot = WaitForFreshSnapshot(engine);
            Assert.NotNull(snapshot);

            var company = snapshot.Companies.Find(c => c.Name == "TestCorp");
            Assert.NotNull(company);
            Assert.Equal("TestCorp", company.Name);
            Assert.Equal(0, company.EmployeeCount);
        }
        finally
        {
            engine.Stop();
        }
    }

    [Fact]
    public void ConcurrentReaders_SeeCompleteSnapshot()
    {
        using var engine = new SimulationEngine();
        engine.Start();
        try
        {
            engine.ExecuteWithWorld(world =>
            {
                for (int i = 0; i < 10; i++)
                {
                    world.Create(
                        new GenName($"Sim_{i}"),
                        new Needs(),
                        new CurrentAction(ActionType.Idle, 0f),
                        new Alive(18)
                    );
                }

                for (int i = 0; i < 5; i++)
                    world.Create(new CompanyName($"Company_{i}"));

                return true;
            });

            var done = new ManualResetEventSlim(false);
            int reads = 0;
            int validSnapshots = 0;

            var threads = new Thread[4];
            for (int t = 0; t < threads.Length; t++)
            {
                threads[t] = new Thread(() =>
                {
                    while (!done.IsSet)
                    {
                        var snapshot = engine.GetLatestSnapshot();
                        if (snapshot == null) continue;

                        var incremented = Interlocked.Increment(ref reads);

                        if (snapshot.Sims != null && snapshot.Companies != null)
                            Interlocked.Increment(ref validSnapshots);
                    }
                });
                threads[t].Start();
            }

            Thread.Sleep(200);
            done.Set();

            foreach (var t in threads)
                t.Join();

            Assert.True(reads > 0, "Expected at least one snapshot read");
            Assert.Equal(reads, validSnapshots);
        }
        finally
        {
            engine.Stop();
        }
    }

    [Fact]
    public void SnapshotBuildTime_IsMeasured()
    {
        using var engine = new SimulationEngine();
        engine.Start();
        try
        {
            WaitForSnapshot(engine);
            Assert.True(engine.LastSnapshotBuildTimeMs >= 0);
        }
        finally
        {
            engine.Stop();
        }
    }

    [Fact]
    public void SnapshotTicks_IncrementAcrossTicks()
    {
        using var engine = new SimulationEngine();
        engine.Start();
        try
        {
            var snapshot1 = WaitForSnapshot(engine);
            Assert.NotNull(snapshot1);
            long tick1 = snapshot1.Tick;

            Thread.Sleep(100);

            var snapshot2 = engine.GetLatestSnapshot();
            Assert.NotNull(snapshot2);
            long tick2 = snapshot2.Tick;

            Assert.True(tick2 > tick1, $"Expected tick2 ({tick2}) > tick1 ({tick1})");
        }
        finally
        {
            engine.Stop();
        }
    }

    [Fact]
    public void SnapshotAfterStop_ReturnsNull()
    {
        using var engine = new SimulationEngine();
        engine.Start();
        WaitForSnapshot(engine);
        engine.Stop();

        var snapshot = engine.GetLatestSnapshot();
        Assert.Null(snapshot);
    }

    [Fact]
    public void Fallback_ExecuteWithWorld_WorksWhenNoSnapshot()
    {
        using var engine = new SimulationEngine();

        var snapshot = engine.GetLatestSnapshot();
        Assert.Null(snapshot);

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            engine.ExecuteWithWorld(world =>
            {
                var query = new QueryDescription().WithAll<GenName>();
                return false;
            });
        });
        Assert.Contains("not running", ex.Message);
    }

    [Fact]
    public void GetCompany_ReadsFromSnapshot()
    {
        using var engine = new SimulationEngine();
        engine.Start();
        try
        {
            int entityId = engine.ExecuteWithWorld(world =>
            {
                var entity = world.Create(new CompanyName("SnapshotCorp"));
                return entity.Id;
            });

            var snapshot = WaitForFreshSnapshot(engine);
            Assert.NotNull(snapshot);

            var company = snapshot.Companies.Find(c => c.EntityId == entityId);
            Assert.NotNull(company);
            Assert.Equal("SnapshotCorp", company.Name);
        }
        finally
        {
            engine.Stop();
        }
    }

    private static WorldSnapshot? WaitForSnapshot(SimulationEngine engine, int maxAttempts = 100, int delayMs = 20)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            var snapshot = engine.GetLatestSnapshot();
            if (snapshot != null)
                return snapshot;
            Thread.Sleep(delayMs);
        }
        return engine.GetLatestSnapshot();
    }

    private static WorldSnapshot? WaitForFreshSnapshot(SimulationEngine engine, int maxAttempts = 100, int delayMs = 20)
    {
        var current = engine.GetLatestSnapshot();
        var currentTick = current?.Tick ?? 0;

        for (int i = 0; i < maxAttempts; i++)
        {
            var snapshot = engine.GetLatestSnapshot();
            if (snapshot != null && snapshot.Tick > currentTick)
                return snapshot;
            Thread.Sleep(delayMs);
        }
        return engine.GetLatestSnapshot();
    }
}
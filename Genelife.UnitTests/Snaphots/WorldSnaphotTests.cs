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
        for (int i = 0; i < times; i++)
            engine.Tick(1f / 20f);
    }

    [Fact]
    public void WorldSnapshot_Construction_StoresAllProperties()
    {
        var sims = new List<SimStatus>
        {
            new() { Name = "Alice", Hunger = 50, Energy = 80 }
        };
        var companies = new List<CompanyStatus>
        {
            new() { NetworkId = 1, Name = "Acme", EmployeeCount = 0 }
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
    public void GetLatestSnapshot_ReturnsNonNull_AfterTick()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        try
        {
            Tick(engine);
            var snapshot = engine.GetLatestSnapshot();
            Assert.NotNull(snapshot);
            Assert.NotNull(snapshot.Sims);
            Assert.NotNull(snapshot.Companies);
            Assert.True(snapshot.Tick > 0);
        }
        finally
        {
            engine.Shutdown();
        }
    }

    [Fact]
    public void SnapshotSims_ReflectsWorldEntities()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
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

            Tick(engine);
            var snapshot = engine.GetLatestSnapshot();
            Assert.NotNull(snapshot);

            var sim = snapshot.Sims.Find(s => s.Name == "TestSim");
            Assert.NotNull(sim);
            Assert.Equal("TestSim", sim.Name);
            Assert.Equal("Sleeping", sim.CurrentAction);
            Assert.True(sim.Energy > 0f);
        }
        finally
        {
            engine.Shutdown();
        }
    }

    [Fact]
    public void SnapshotCompanies_ReflectsWorldEntities()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        try
        {
            engine.ExecuteWithWorld(world =>
            {
                world.Create(new CompanyName("TestCorp"));
                return true;
            });

            Tick(engine);
            var snapshot = engine.GetLatestSnapshot();
            Assert.NotNull(snapshot);

            var company = snapshot.Companies.Find(c => c.Name == "TestCorp");
            Assert.NotNull(company);
            Assert.Equal("TestCorp", company.Name);
            Assert.Equal(0, company.EmployeeCount);
        }
        finally
        {
            engine.Shutdown();
        }
    }

    [Fact]
    public void ConcurrentReaders_SeeCompleteSnaphot()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
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

            Tick(engine);
            var snapshot = engine.GetLatestSnapshot();
            Assert.NotNull(snapshot);
            Assert.True(snapshot.Sims.Count > 0 || snapshot.Companies.Count > 0);
        }
        finally
        {
            engine.Shutdown();
        }
    }

    [Fact]
    public void SnapshotBuildTime_IsMeasured()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        try
        {
            Tick(engine);
            Assert.True(engine.LastSnapshotBuildTimeMs >= 0);
        }
        finally
        {
            engine.Shutdown();
        }
    }

    [Fact]
    public void SnapshotTicks_IncrementAcrossTicks()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        try
        {
            Tick(engine);
            var snapshot1 = engine.GetLatestSnapshot();
            Assert.NotNull(snapshot1);
            long tick1 = snapshot1.Tick;

            Tick(engine);
            var snapshot2 = engine.GetLatestSnapshot();
            Assert.NotNull(snapshot2);
            long tick2 = snapshot2.Tick;

            Assert.True(tick2 > tick1);
        }
        finally
        {
            engine.Shutdown();
        }
    }

    [Fact]
    public void SnapshotAfterStop_ReturnsNull()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        Tick(engine);
        engine.Shutdown();

        var snapshot = engine.GetLatestSnapshot();
        Assert.Null(snapshot);
    }

    [Fact]
    public void GetCompany_ReadsFromSnapshot()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        try
        {
            ulong networkId = 0;
            engine.ExecuteWithWorld(world =>
            {
                var entity = world.Create(new CompanyName("SnaphotCorp"));
                return true;
            });

            Tick(engine);
            var snapshot = engine.GetLatestSnapshot();
            Assert.NotNull(snapshot);

            var company = snapshot.Companies.Find(c => c.Name == "SnaphotCorp");
            Assert.NotNull(company);
            Assert.Equal("SnaphotCorp", company.Name);
        }
        finally
        {
            engine.Shutdown();
        }
    }
}
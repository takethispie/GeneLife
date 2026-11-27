using Arch.Core;
using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Enums;
using Genelife.Map;
using Genelife.Values;

namespace Genelife.UnitTests.Snapshots;

/// <summary>
/// Covers the two building links exposed on <see cref="GenosStatus"/> so the client can show
/// who works in and who lives in a selected building. Both are projected from components that
/// already exist — <see cref="Employee.Office"/> for the workplace, and
/// <c>Residence -&gt; HousingUnit.Property</c> for the home — and must be null (the client's
/// "unknown") rather than 0 when the link cannot be resolved.
/// </summary>
public class GenosBuildingSnapshotTests
{
    private static void Tick(SimulationEngine engine, int times = 1)
    {
        engine.SnapshotsEnabled = true;
        for (var i = 0; i < times; i++)
            engine.Tick(1f / 20f);
    }

    private static GenosStatus Find(SimulationEngine engine, string name)
    {
        var snapshot = engine.GetLatestSnapshot();
        Assert.NotNull(snapshot);
        var genos = snapshot.Genoses.Find(status => status.Name == name);
        Assert.NotNull(genos);
        return genos;
    }

    private static (Entity Office, ulong Id) CreateOffice(
        SimulationEngine engine, World world, Entity company, int x, int y, bool placed = true)
    {
        var office = placed
            ? world.Create(
                new Office("Test Employer HQ", company),
                new Placement(new TileCoord(x, y), new Footprint(2, 2), new TileCoord(x, y + 2), BuildingKind.Office))
            : world.Create(new Office("Test Employer HQ", company));
        return (office, engine.NetworkIds.Allocate(office).Value);
    }

    [Fact]
    public void EmployedGenos_PointsAtItsOfficeBuilding()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        try
        {
            ulong? officeId = null;
            engine.ExecuteWithWorld(world =>
            {
                var company = world.Create(new CompanyName("Test Employer"));
                engine.NetworkIds.Allocate(company);
                var (office, id) = CreateOffice(engine, world, company, 10, 10);
                officeId = id;

                world.Create(
                    new GenName("Employed"),
                    new Needs(),
                    new CurrentAction(ActionType.Idle, 0f),
                    new Alive(30),
                    new Employee(company, office, "Clerk", 100m));
                return true;
            });

            Tick(engine);

            var genos = Find(engine, "Employed");
            Assert.Equal(officeId, genos.WorkplaceBuildingNetworkId);
            // No Residence component: homeless, so the housing link stays unknown.
            Assert.Null(genos.HousingBuildingNetworkId);
        }
        finally { engine.Shutdown(); }
    }

    [Fact]
    public void UnemployedGenos_HasNullWorkplaceBuilding()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        try
        {
            engine.ExecuteWithWorld(world =>
            {
                world.Create(
                    new GenName("Unemployed"),
                    new Needs(),
                    new CurrentAction(ActionType.Idle, 0f),
                    new Alive(30));
                return true;
            });

            Tick(engine);

            Assert.Null(Find(engine, "Unemployed").WorkplaceBuildingNetworkId);
        }
        finally { engine.Shutdown(); }
    }

    [Fact]
    public void HousedGenos_PointsAtItsUnitBuilding()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        try
        {
            ulong? propertyId = null;
            engine.ExecuteWithWorld(world =>
            {
                var property = world.Create(
                    new Property("Test Housing Block", "Central", Entity.Null, 1),
                    new Placement(new TileCoord(20, 20), new Footprint(3, 3), new TileCoord(20, 23), BuildingKind.Residential));
                propertyId = engine.NetworkIds.Allocate(property).Value;

                var unit = world.Create(
                    new HousingUnit(property, "1A", HousingTier.Basic, 4, Money.FromUnits(500m), isVacant: false));
                engine.NetworkIds.Allocate(unit);

                world.Create(
                    new GenName("Housed"),
                    new Needs(),
                    new CurrentAction(ActionType.Idle, 0f),
                    new Alive(30),
                    new Residence(Entity.Null, unit));
                return true;
            });

            Tick(engine);

            var genos = Find(engine, "Housed");
            Assert.Equal(propertyId, genos.HousingBuildingNetworkId);
            Assert.Null(genos.WorkplaceBuildingNetworkId);
        }
        finally { engine.Shutdown(); }
    }

    [Fact]
    public void HomelessGenos_HasNullHousingBuilding()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        try
        {
            engine.ExecuteWithWorld(world =>
            {
                world.Create(
                    new GenName("Homeless"),
                    new Needs(),
                    new CurrentAction(ActionType.Idle, 0f),
                    new Alive(30));
                return true;
            });

            Tick(engine);

            Assert.Null(Find(engine, "Homeless").HousingBuildingNetworkId);
        }
        finally { engine.Shutdown(); }
    }

    [Fact]
    public void UnplacedOrDestroyedBuilding_YieldsNull_NotZero()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        try
        {
            engine.ExecuteWithWorld(world =>
            {
                var company = world.Create(new CompanyName("Ghost Employer"));
                engine.NetworkIds.Allocate(company);

                // Registered but never placed on the map: not a building the client can select.
                var (unplaced, _) = CreateOffice(engine, world, company, 0, 0, placed: false);
                world.Create(
                    new GenName("UnplacedWorker"),
                    new Needs(),
                    new CurrentAction(ActionType.Idle, 0f),
                    new Alive(30),
                    new Employee(company, unplaced, "Clerk", 100m));

                // Placed, then destroyed: the id is gone and must not leak through.
                var (destroyed, _) = CreateOffice(engine, world, company, 40, 40);
                world.Create(
                    new GenName("OrphanWorker"),
                    new Needs(),
                    new CurrentAction(ActionType.Idle, 0f),
                    new Alive(30),
                    new Employee(company, destroyed, "Clerk", 100m));
                engine.NetworkIds.Remove(destroyed);
                world.Destroy(destroyed);

                return true;
            });

            Tick(engine);

            Assert.Null(Find(engine, "UnplacedWorker").WorkplaceBuildingNetworkId);
            Assert.Null(Find(engine, "OrphanWorker").WorkplaceBuildingNetworkId);
        }
        finally { engine.Shutdown(); }
    }
}
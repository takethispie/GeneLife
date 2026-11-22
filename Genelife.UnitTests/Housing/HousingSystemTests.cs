using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Api.Repositories;
using Genelife.Api.Services;
using Genelife.Components.Company;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Housing;
using Genelife.Systems;
using Genelife.Values;

namespace Genelife.UnitTests.Housing;

public class HousingSystemTests
{
    [Fact]
    public void Rent_charges_once_and_clears_arrears_after_full_payment()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var (_, unit) = TestHousing.CreateUnit(world, economy, 10, 11);
        var sim = TestHousing.Sim(world, 1, 1000m);
        var household = TestHousing.Household(world, economy.Housing, sim, unit, 120m);
        var system = new RentSystem(world, economy);

        var before = world.Get<Wallet>(sim).Balance;
        system.Update(100, SimulationClock.Default);

        var after = world.Get<Wallet>(sim).Balance;
        Assert.True(after < before);
        Assert.True(economy.Government.Balance.IsPositive);
        Assert.Equal(Money.Zero, world.Get<Household>(household).RentArrears);
        Assert.Equal(100, world.Get<Household>(household).LastRentTick);

        var paidOnce = world.Get<Wallet>(sim).Balance;
        system.Update(100, SimulationClock.Default);
        Assert.Equal(paidOnce, world.Get<Wallet>(sim).Balance);

        world.Get<Wallet>(sim).Balance = Money.Zero;
        system.Update(200, SimulationClock.Default);
        var missed = world.Get<Household>(household);
        Assert.True(missed.RentArrears.IsPositive);
        Assert.Equal(1, missed.ArrearsCycles);
        Assert.True(missed.UtilityArrears.IsPositive);

        world.Get<Wallet>(sim).Balance = new Money(500_00);
        system.Update(300, SimulationClock.Default);
        var cleared = world.Get<Household>(household);
        Assert.Equal(Money.Zero, cleared.RentArrears);
        Assert.Equal(0, cleared.ArrearsCycles);
    }

    [Fact]
    public void Maintenance_repairs_when_funded_and_decays_when_not()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var (property, unit) = TestHousing.CreateUnit(world, economy, 10, 11);
        var propertyData = world.Get<Property>(property);
        propertyData.Condition = 0.5f;
        world.Get<Property>(property) = propertyData;
        var unitData = world.Get<HousingUnit>(unit);
        unitData.Condition = 0.5f;
        world.Get<HousingUnit>(unit) = unitData;

        economy.Government.Balance = new Money(10_000_00);
        var system = new MaintenanceSystem(world, economy);
        system.Update(100);
        Assert.True(world.Get<Property>(property).Condition > 0.5f);

        economy.Government.Balance = Money.Zero;
        var before = world.Get<Property>(property).Condition;
        system.Update(200);
        Assert.True(world.Get<Property>(property).Condition < before);
    }

    [Fact]
    public void Housing_modifier_applies_tier_profile_and_homeless_penalty()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var (_, unit) = TestHousing.CreateUnit(world, economy, 10, 11, HousingTier.Basic);
        var sim = TestHousing.Sim(world, 1, 500m);
        var household = TestHousing.Household(world, economy.Housing, sim, unit, 120m);
        var system = new HousingModifierSystem(world, economy.Housing);

        system.Update();
        Assert.True(world.Has<NeedsModifier>(sim));
        var basic = world.Get<NeedsModifier>(sim);
        Assert.Equal(0.8f, basic.SleepRecovery);
        Assert.False(basic.IsHomeless);

        economy.Housing.MoveOut(unit, household);
        system.Update();
        Assert.True(world.Get<NeedsModifier>(sim).IsHomeless);
        Assert.True(world.Get<NeedsModifier>(sim).HealthDrift < 0f);
    }

    [Fact]
    public void Eviction_ladder_relocates_before_homelessness()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var (_, oldUnit) = TestHousing.CreateUnit(world, economy, 10, 11);
        var (_, newUnit) = TestHousing.CreateUnit(world, economy, 20, 21);
        var sim = TestHousing.Sim(world, 1, 1000m);
        var household = TestHousing.Household(world, economy.Housing, sim, oldUnit, 120m);

        var data = world.Get<Household>(household);
        data.ArrearsCycles = 3;
        data.RentArrears = new Money(300_00);
        data.NoticeStage = 3;
        data.CureDeadlineTick = 50;
        world.Get<Household>(household) = data;

        new EvictionSystem(world, economy).Update(200);

        Assert.Equal(Entity.Null, economy.Housing.HouseholdIn(oldUnit));
        Assert.True(world.Get<HousingUnit>(oldUnit).IsVacant);
        Assert.NotEqual(oldUnit, world.Get<Household>(household).Unit);
        Assert.Equal(Money.Zero, world.Get<Household>(household).RentArrears);
        Assert.Contains(economy.DrainEvents(), e => e is Genelife.Messages.Events.TenantEvicted);
    }

    [Fact]
    public void Market_index_falls_when_vacancy_is_high_and_rises_when_tight()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        for (var i = 0; i < 10; i++)
            TestHousing.CreateUnit(world, economy, (ulong)(100 + i), (ulong)(200 + i));

        var system = new HousingMarketSystem(world, economy);
        system.Update(100, SimulationClock.Default);
        Assert.True(economy.Housing.Market.Index < 1m);

        var unitQuery = new QueryDescription().WithAll<HousingUnit>();
        world.Query(in unitQuery, (ref HousingUnit unit) =>
        {
            unit.IsVacant = false;
        });

        var before = economy.Housing.Market.Index;
        system.Update(200, SimulationClock.Default);
        Assert.True(economy.Housing.Market.Index > before);
    }

    [Fact]
    public void Construction_pays_in_instalments_and_creates_units()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        economy.HousingOptions.ConstructionMonths = 2;
        economy.HousingOptions.MaxActiveConstructionProjects = 1;

        var developer = world.Create(new CompanyName("Builder"));
        world.Add(developer, new NetworkId(500));
        var account = CompanyAccount.Create(new Money(200_000_00), 0);
        account.State = CompanySolvency.Healthy;
        world.Add(developer, account);

        var system = new HousingDevelopmentSystem(world, economy);
        system.StartProject(developer, "Central", HousingTier.Basic, 2, 0);
        Assert.Equal(1, CountProjects(world));

        system.Update(100);
        Assert.Equal(1, CountProjects(world));
        system.Update(200);

        Assert.Equal(0, CountProjects(world));
        Assert.Equal(2, economy.Housing.UnitsByProperty.Values.Sum(list => list.Count));
        Assert.Equal(2, economy.Housing.Market.CompletionsThisYear);
    }

    [Fact]
    public void Purchase_transfers_ownership_and_requires_no_arrears()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        var assigner = new HouseholdAssigner(engine.World!, engine.NetworkIds, engine.Economy!);
        var plan = new PopulationPlan
        {
            Adults = 2,
            Children = 0,
            ExpectedMonthlyIncome = 400m,
            BasicUnits = 2,
            ShelterBeds = 1
        };
        assigner.Spawn(plan, seed: 7, tick: 0);

        var household = engine.BuildHouseholds().First(h => !h.Homeless && h.Tenure == "Renter");
        var unit = engine.BuildHousingProperties()
            .SelectMany(p => p.Units)
            .First(u => u.NetworkId == household.UnitNetworkId);

        var networkIds = engine.NetworkIds;
        networkIds.TryGetEntity(new NetworkId(household.HeadNetworkId), out var head);
        engine.World!.Get<Wallet>(head).Balance = new Money(1_000_000_00);

        var repository = new HousingRepository(engine.World!, networkIds, engine.Economy!);
        Assert.True(repository.Purchase(household.NetworkId, unit.NetworkId, 0));

        var updated = engine.BuildHouseholds().First(h => h.NetworkId == household.NetworkId);
        Assert.Equal("Owner", updated.Tenure);
        Assert.Equal(0m, updated.MonthlyRent);

        var data = engine.World!.Get<Household>(engine.NetworkIds.TryGetEntity(new NetworkId(household.NetworkId), out var householdEntity) ? householdEntity : Entity.Null);
        data.RentArrears = new Money(10_00);
        engine.World!.Get<Household>(householdEntity) = data;
        Assert.Throws<InvalidOperationException>(() =>
            repository.Purchase(household.NetworkId, unit.NetworkId, 0));
    }

    [Fact]
    public void Full_tick_path_runs_housing_systems_without_exceptions()
    {
        using var engine = new SimulationEngine { RandomSeed = 5 };
        engine.Initialize();
        new HouseholdAssigner(engine.World!, engine.NetworkIds, engine.Economy!).Spawn(
            new PopulationPlan
            {
                Adults = 18,
                Children = 6,
                ExpectedMonthlyIncome = 420m,
                BasicUnits = 12,
                ComfortableUnits = 4,
                ShelterBeds = 2
            },
            seed: 5,
            tick: 0);

        for (var i = 0; i < 350; i++)
            engine.Tick(1f / 20f);

        var housing = engine.BuildHousingStatus();
        Assert.True(housing.Units > 0);
        Assert.True(housing.Properties > 0);
        Assert.Empty(engine.Economy!.Housing.Verify());
    }

    [Fact]
    public void Persistence_round_trip_preserves_housing_and_verifies_registry()
    {
        using var engine = new SimulationEngine { RandomSeed = 42 };
        engine.Initialize();
        new HouseholdAssigner(engine.World!, engine.NetworkIds, engine.Economy!).Spawn(
            new PopulationPlan
            {
                Adults = 20,
                Children = 8,
                ExpectedMonthlyIncome = 400m,
                BasicUnits = 12,
                ComfortableUnits = 5,
                ShelterBeds = 3
            },
            seed: 42,
            tick: 0);

        var snapshot = engine.CaptureState(engine.NetworkIds);
        var units = snapshot.HousingUnits.Count;
        var households = snapshot.Households.Count;
        var rentRoll = snapshot.Households.Sum(h => h.MonthlyRentCents);

        using var restored = new SimulationEngine();
        restored.Restore(snapshot, restored.NetworkIds);

        Assert.Equal(units, restored.Economy!.Housing.UnitsByProperty.Values.Sum(list => list.Count));
        Assert.Equal(households, restored.Economy.Housing.UnitByHousehold.Count);

        var restoredHouseholds = new List<Household>();
        restored.World!.Query(in new QueryDescription().WithAll<Household>(),
            (ref Household household) => restoredHouseholds.Add(household));
        Assert.Equal(rentRoll, restoredHouseholds.Sum(h => h.MonthlyRent.Cents));
        Assert.Empty(restored.Economy.Housing.Verify());
    }

    [Fact]
    public void Old_snapshot_migration_assigns_existing_adults_to_housing()
    {
        using var engine = new SimulationEngine { RandomSeed = 9 };
        engine.Initialize();
        new HouseholdAssigner(engine.World!, engine.NetworkIds, engine.Economy!).Spawn(
            new PopulationPlan { Adults = 10, Children = 2, BasicUnits = 6, ShelterBeds = 2 },
            seed: 9,
            tick: 0);

        var snapshot = engine.CaptureState(engine.NetworkIds);
        snapshot.HousingMigrated = false;
        snapshot.Properties.Clear();
        snapshot.HousingUnits.Clear();
        snapshot.Households.Clear();
        snapshot.HouseholdMembers.Clear();
        snapshot.ConstructionProjects.Clear();
        snapshot.HousingMarket = new Genelife.Api.Persistence.PersistedHousingMarket();

        using var restored = new SimulationEngine();
        restored.Restore(snapshot, restored.NetworkIds);

        var migratedHouseholds = new List<Household>();
        restored.World!.Query(in new QueryDescription().WithAll<Household>(),
            (ref Household household) => migratedHouseholds.Add(household));
        Assert.NotEmpty(migratedHouseholds);
        Assert.NotEmpty(restored.Economy!.Housing.UnitsByProperty);
        Assert.True(migratedHouseholds.Any(h => h.Unit != Entity.Null));
        Assert.Empty(restored.Economy.Housing.Verify());
    }

    private static int CountProjects(World world)
    {
        var count = 0;
        world.Query(in new QueryDescription().WithAll<ConstructionProject>(), (ref ConstructionProject _) => count++);
        return count;
    }
}

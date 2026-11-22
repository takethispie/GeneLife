using Arch.Core;
using Arch.Core.Extensions;
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

/// <summary>
/// Reduced-scale deterministic housing scenarios. The harness runs the real
/// monthly systems in production order, so the tests cover interactions rather
/// than isolated formulas. Sizes are kept small enough for the regular suite.
/// </summary>
public class HousingScenarioTests
{
    [Fact]
    public void Stable_income_keeps_most_households_housed()
    {
        using var scenario = ScenarioHarness.Create();
        scenario.Spawn(new PopulationPlan
        {
            Adults = 60,
            Children = 20,
            ExpectedMonthlyIncome = 400m,
            BasicUnits = 40,
            ComfortableUnits = 8,
            ShelterBeds = 4
        }, seed: 11);
        scenario.RunMonths(24, monthlyIncomePerAdult: 200m);

        var stats = scenario.Stats();
        Assert.True(stats.Housed + stats.Sheltered >= (int)Math.Ceiling(stats.Households * 0.75),
            stats.ToString());
        Assert.True(stats.Arrears < stats.Households * 200m, stats.ToString());
        Assert.Empty(scenario.Economy.Housing.Verify());
    }

    [Fact]
    public void Income_shock_raises_arrears_evictions_and_shelter_use()
    {
        using var scenario = ScenarioHarness.Create();
        scenario.Spawn(new PopulationPlan
        {
            Adults = 60,
            Children = 20,
            ExpectedMonthlyIncome = 400m,
            BasicUnits = 26,
            ShelterBeds = 4
        }, seed: 12);
        scenario.RunMonths(24, monthlyIncomePerAdult: 0m);

        var stats = scenario.Stats();
        Assert.True(stats.Arrears > 0m || stats.PeakArrears > 0m, stats.ToString());
        Assert.True(stats.Evictions > 0, stats.ToString());
        Assert.True(stats.Homeless + stats.Sheltered > 0, stats.ToString());
        Assert.Empty(scenario.Economy.Housing.Verify());
    }

    [Fact]
    public void Low_vacancy_triggers_company_construction()
    {
        using var scenario = ScenarioHarness.Create();
        scenario.Spawn(new PopulationPlan
        {
            Adults = 16,
            Children = 4,
            ExpectedMonthlyIncome = 400m,
            BasicUnits = 8,
            ShelterBeds = 2
        }, seed: 13);
        scenario.AddDeveloper(new Money(200_000_00));
        scenario.Economy.HousingOptions.ConstructionMonths = 4;
        scenario.Economy.HousingOptions.ConstructionVacancyTrigger = 0.05m;

        var before = scenario.UnitCount;
        scenario.RunMonths(12, monthlyIncomePerAdult: 180m, runDevelopment: true);

        Assert.True(scenario.UnitCount > before, $"units {before} -> {scenario.UnitCount}");
        Assert.Empty(scenario.Economy.Housing.Verify());
    }

    [Fact]
    public void Shelter_absorbs_households_with_no_stock_and_remains_consistent()
    {
        using var scenario = ScenarioHarness.Create();
        scenario.Spawn(new PopulationPlan
        {
            Adults = 20,
            Children = 6,
            ExpectedMonthlyIncome = 400m,
            BasicUnits = 0,
            ShelterBeds = 3
        }, seed: 14);
        scenario.RunMonths(6, monthlyIncomePerAdult: 0m);

        var stats = scenario.Stats();
        Assert.True(stats.Sheltered + stats.Homeless > 0, stats.ToString());
        Assert.Equal(stats.Households, stats.Housed + stats.Sheltered + stats.Homeless);
        Assert.Empty(scenario.Economy.Housing.Verify());
    }
}

internal sealed class ScenarioHarness : IDisposable
{
    private readonly World world;
    private readonly NetworkIdRegistry networkIds;
    private readonly RentSystem rent;
    private readonly HousingMoveSystem moves;
    private readonly HousingMarketSystem market;
    private readonly MaintenanceSystem maintenance;
    private readonly EvictionSystem evictions;
    private readonly HousingModifierSystem modifiers;
    private readonly HousingDevelopmentSystem development;
    private readonly HousingMetricsSystem metrics;
    private readonly HouseholdFinanceService finance;
    private long tick;
    private int evictionCount;
    private decimal peakArrears;

    private ScenarioHarness()
    {
        world = World.Create();
        networkIds = new NetworkIdRegistry();
        Economy = new EconomyContext(world);
        Economy.Government.Balance = new Money(1_000_000_00);
        Assigner = new HouseholdAssigner(world, networkIds, Economy);
        rent = new RentSystem(world, Economy);
        moves = new HousingMoveSystem(world, Economy);
        market = new HousingMarketSystem(world, Economy);
        maintenance = new MaintenanceSystem(world, Economy);
        evictions = new EvictionSystem(world, Economy);
        modifiers = new HousingModifierSystem(world, Economy.Housing);
        development = new HousingDevelopmentSystem(world, Economy);
        metrics = new HousingMetricsSystem(world, Economy);
        finance = new HouseholdFinanceService(world, Economy);
    }

    public EconomyContext Economy { get; }
    public HouseholdAssigner Assigner { get; }

    public static ScenarioHarness Create() => new();

    public void Spawn(PopulationPlan plan, int seed) =>
        Assigner.Spawn(plan, seed, 0);

    public void AddDeveloper(Money capital)
    {
        var developer = world.Create(new CompanyName("Scenario Builder"));
        var networkId = networkIds.Allocate(developer);
        world.Add(developer, networkId);
        var account = CompanyAccount.Create(capital, 0);
        account.State = CompanySolvency.Healthy;
        world.Add(developer, account);
    }

    public void RunMonths(int months, decimal monthlyIncomePerAdult, bool runDevelopment = false)
    {
        for (var i = 0; i < months; i++)
        {
            tick += Economy.Clock.TicksPerMonth;
            TopUp(monthlyIncomePerAdult);
            modifiers.Update();
            rent.Update(tick, Economy.Clock);
            peakArrears = Math.Max(peakArrears, CurrentArrears());
            moves.Update(tick);
            market.Update(tick, Economy.Clock);
            maintenance.Update(tick);
            evictions.Update(tick);
            if (runDevelopment)
                development.Update(tick);
            if (Economy.Clock.IsYearBoundary(tick))
                metrics.Update(tick);

            evictionCount += Economy.DrainEvents().Count(e => e is Genelife.Messages.Events.TenantEvicted);
        }
    }

    public int UnitCount =>
        new QueryDescription().WithAll<HousingUnit>() is var query
            ? Count(query)
            : 0;

    public ScenarioStats Stats()
    {
        var households = 0;
        var housed = 0;
        var sheltered = 0;
        var homeless = 0;
        var arrears = 0m;
        world.Query(in new QueryDescription().WithAll<Household>(), (ref Household household) =>
        {
            households++;
            if (household.Unit == Entity.Null)
                homeless++;
            else if (household.Tenure == Tenure.Sheltered)
                sheltered++;
            else
                housed++;
            arrears += household.RentArrears.Units;
        });

        return new ScenarioStats(households, housed, sheltered, homeless, arrears, evictionCount, peakArrears);
    }

    private decimal CurrentArrears()
    {
        var total = 0m;
        world.Query(in new QueryDescription().WithAll<Household>(), (ref Household household) =>
            total += household.RentArrears.Units);
        return total;
    }

    private void TopUp(decimal monthlyIncomePerAdult)
    {
        if (monthlyIncomePerAdult <= 0m)
            return;

        world.Query(in new QueryDescription().WithAll<Household>(), (Entity entity, ref Household household) =>
        {
            var adults = finance.AdultMembers(entity);
            foreach (var adult in adults)
            {
                finance.Credit(
                    adult,
                    Money.FromUnits(monthlyIncomePerAdult),
                    LedgerKind.WagesPaid,
                    tick,
                    null,
                    "scenario.income");
            }
        });
    }

    private int Count(in QueryDescription query)
    {
        var count = 0;
        world.Query(in query, (ref HousingUnit _) => count++);
        return count;
    }

    public void Dispose()
    {
        world.Dispose();
    }
}

internal sealed record ScenarioStats(
    int Households,
    int Housed,
    int Sheltered,
    int Homeless,
    decimal Arrears,
    int Evictions,
    decimal PeakArrears)
{
    public override string ToString() =>
        $"households={Households} housed={Housed} sheltered={Sheltered} homeless={Homeless} " +
        $"arrears={Arrears:N2} peak={PeakArrears:N2} evictions={Evictions}";
}

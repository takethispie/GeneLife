using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Housing;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Systems;

/// <summary>
/// Yearly housing aggregation. The scan is O(units + households) once per
/// simulated year, never per tick.
/// </summary>
public class HousingMetricsSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly HousingRegistry registry;
    private readonly HouseholdFinanceService finance;
    private readonly int interval;

    public HousingMetricsSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        registry = economy.Housing;
        finance = new HouseholdFinanceService(world, economy);
        this.interval = interval ?? Math.Max(1, economy.Clock.TicksPerYear);
    }

    public string Name => "HousingMetrics";
    public int Order => 611;
    public Cadence Cadence => new(interval);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public HousingSnapshot Update(long tick)
    {
        var properties = 0;
        var units = 0;
        var occupied = 0;
        var vacant = 0;
        var shelterBeds = 0;
        var shelterOccupied = 0;
        var overcrowded = 0;
        var belowCondition = 0;

        var propertyQuery = new QueryDescription().WithAll<Property>();
        world.Query(in propertyQuery, (ref Property _) => properties++);

        var unitQuery = new QueryDescription().WithAll<HousingUnit>();
        world.Query(in unitQuery, (ref HousingUnit unit) =>
        {
            units++;
            if (unit.IsVacant)
                vacant++;
            else
                occupied++;
            if (unit.Tier == HousingTier.Shelter)
            {
                shelterBeds++;
                if (!unit.IsVacant)
                    shelterOccupied++;
            }
            if (unit.IsOvercrowded)
                overcrowded++;
            if (unit.Condition < 0.2f)
                belowCondition++;
        });

        var sheltered = 0;
        var homeless = 0;
        var rentArrears = Money.Zero;
        var utilityArrears = Money.Zero;
        var burdens = new List<decimal>();

        var householdQuery = new QueryDescription().WithAll<Household>();
        world.Query(in householdQuery, (Entity entity, ref Household household) =>
        {
            if (household.Tenure == Tenure.Sheltered)
                sheltered++;
            if (household.Unit == Entity.Null)
                homeless++;
            rentArrears += household.RentArrears;
            utilityArrears += household.UtilityArrears;
            if (household.Tenure == Tenure.Renter)
            {
                var income = finance.MonthlyIncome(entity);
                if (income.IsPositive)
                    burdens.Add(household.MonthlyRent.Cents / (decimal)income.Cents);
            }
        });

        var constructionUnits = 0;
        var projectQuery = new QueryDescription().WithAll<ConstructionProject>();
        world.Query(in projectQuery, (ref ConstructionProject project) => constructionUnits += project.Units);

        var vacancyRate = units > 0 ? vacant / (decimal)units : 0m;
        var shelterOccupancy = shelterBeds > 0 ? shelterOccupied / (decimal)shelterBeds : 0m;

        var snapshot = new HousingSnapshot(
            tick,
            economy.Clock.YearOf(tick),
            properties,
            units,
            occupied,
            vacant,
            shelterBeds,
            sheltered,
            homeless,
            overcrowded,
            belowCondition,
            vacancyRate,
            registry.Market.Index,
            registry.TotalRentRoll(),
            registry.Market.RentCollectedThisYear,
            rentArrears,
            utilityArrears,
            registry.Market.UpkeepPaidThisYear,
            registry.Market.EvictionsThisYear,
            constructionUnits,
            Median(burdens),
            shelterOccupancy);

        registry.MetricHistory.Add(snapshot);
        if (registry.MetricHistory.Count > 50)
            registry.MetricHistory.RemoveAt(0);

        economy.Emit(new HousingSnapshotTaken(
            snapshot.Tick,
            snapshot.Year,
            snapshot.Properties,
            snapshot.Units,
            snapshot.OccupiedUnits,
            snapshot.VacantUnits,
            snapshot.ShelterBeds,
            snapshot.ShelteredHouseholds,
            snapshot.HomelessHouseholds,
            snapshot.OvercrowdedUnits,
            snapshot.UnitsBelowCondition,
            snapshot.VacancyRate,
            snapshot.RentIndex,
            snapshot.RentRoll.Cents,
            snapshot.RentCollected.Cents,
            snapshot.RentArrears.Cents,
            snapshot.UtilityArrears.Cents,
            snapshot.UpkeepPaid.Cents,
            snapshot.EvictionsThisYear,
            snapshot.ConstructionUnits,
            snapshot.MedianRentBurden,
            snapshot.ShelterOccupancy));

        registry.Market.ResetYearly();
        return snapshot;
    }

    private static decimal Median(List<decimal> values)
    {
        if (values.Count == 0)
            return 0m;
        values.Sort();
        var middle = values.Count / 2;
        return values.Count % 2 == 0
            ? (values[middle - 1] + values[middle]) / 2m
            : values[middle];
    }
}

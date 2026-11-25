using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Housing;
using Genelife.Messages.Events;

namespace Genelife.Systems;

/// <summary>
/// Rent index response to vacancy. Existing tenancies adjust only at the year
/// boundary and never by more than the annual cap.
/// </summary>
public class HousingMarketSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly HousingRegistry registry;
    private readonly HousingAllocator allocator;
    private readonly int interval;

    public HousingMarketSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        registry = economy.Housing;
        allocator = new HousingAllocator(world, economy);
        this.interval = interval ?? Math.Max(1, economy.Clock.TicksPerMonth);
    }

    public string Name => "HousingMarket";
    public int Order => 514;
    public Cadence Cadence => new(interval);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick, context.Time);

    public void Update(long tick) => Update(tick, economy.Clock);

    public void Update(long tick, SimulationClock clock)
    {
        var total = 0;
        var vacant = 0;
        var unitQuery = new QueryDescription().WithAll<HousingUnit>();
        world.Query(in unitQuery, (ref HousingUnit unit) =>
        {
            if (unit.IsCondemned)
                return;
            total++;
            if (unit.IsVacant)
                vacant++;
        });

        var vacancyRate = total > 0 ? vacant / (decimal)total : 0m;
        var options = economy.HousingOptions;
        var step = Math.Clamp(
            (options.TargetVacancyRate - vacancyRate) * options.VacancyResponse,
            -options.MaxIndexStep,
            options.MaxIndexStep);
        var index = Math.Clamp(registry.Market.Index + step, options.MinRentIndex, options.MaxRentIndex);
        registry.Market.Index = index;
        registry.Market.LastUpdatedTick = tick;

        if (clock.IsYearBoundary(tick))
            AdjustExistingRents(tick, options);

        economy.Emit(new HousingMarketUpdated(index, vacancyRate, vacant, total, tick));
    }

    private void AdjustExistingRents(long tick, HousingOptions options)
    {
        var households = new List<Entity>();
        var query = new QueryDescription().WithAll<Household>();
        world.Query(in query, (Entity entity, ref Household household) =>
        {
            if (household.Tenure == Tenure.Renter && household.Unit != Entity.Null)
                households.Add(entity);
        });
        households.Sort((a, b) => a.Id.CompareTo(b.Id));

        foreach (var entity in households)
        {
            if (!world.IsAlive(entity))
                continue;

            var data = world.Get<Household>(entity);
            var target = allocator.MarketRent(data.Unit);
            var cap = data.MonthlyRent.Multiply(options.AnnualRentAdjustmentCap);
            var lower = data.MonthlyRent - cap;
            var upper = data.MonthlyRent + cap;
            data.MonthlyRent = target.Max(lower).Min(upper);
            world.Get<Household>(entity) = data;
        }
    }
}

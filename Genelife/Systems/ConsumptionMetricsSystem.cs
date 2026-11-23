using Arch.Core;
using Genelife.Components.Consumption;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Messages.Events;

namespace Genelife.Systems;

/// <summary>
/// Yearly consumption operating picture. It reads current state and the YTD
/// counters; it never mutates households, meters or stock.
/// </summary>
public sealed class ConsumptionMetricsSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly int interval;

    public ConsumptionMetricsSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? economy.Clock.TicksPerYear;
    }

    public string Name => "ConsumptionMetrics";
    public int Order => 632;
    public Cadence Cadence => new(Math.Max(1, interval));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var water = Network(UtilityService.Water);
        var electricity = Network(UtilityService.Electricity);

        var households = 0;
        var cutoff = 0;
        var reduced = 0;
        world.Query(in new QueryDescription().WithAll<HouseholdMeter>(),
            (ref HouseholdMeter meter) =>
            {
                households++;
                if (meter.WaterLevel == ServiceLevel.CutOff || meter.ElectricityLevel == ServiceLevel.CutOff) cutoff++;
                else if (meter.WaterLevel == ServiceLevel.Reduced || meter.ElectricityLevel == ServiceLevel.Reduced) reduced++;
            });

        var stores = new HashSet<Entity>();
        var stock = 0;
        world.Query(in new QueryDescription().WithAll<StoreInventory, StoreStockLink>(),
            (ref StoreInventory inventory, ref StoreStockLink link) =>
            {
                stores.Add(link.Store);
                stock += Math.Max(0, inventory.Stock);
            });

        var malnourished = 0;
        world.Query(in new QueryDescription().WithAll<NutritionState>(),
            (ref NutritionState nutrition) =>
            {
                if (nutrition.Balance < 0) malnourished++;
            });

        economy.Emit(new ConsumptionMetricsRecorded(
            tick,
            economy.Clock.YearOf(tick),
            water.Production, water.Capacity, water.Load,
            electricity.Production, electricity.Capacity, electricity.Load,
            households,
            cutoff,
            reduced,
            stores.Count,
            stock,
            0,
            malnourished,
            economy.Housing.Market.UtilityCollectedThisYear.Cents,
            economy.ConsumptionThisYear.Cents));
    }

    private UtilityNetwork Network(UtilityService service)
    {
        var found = default(UtilityNetwork);
        world.Query(in new QueryDescription().WithAll<UtilityNetwork>(),
            (ref UtilityNetwork network) =>
            {
                if (network.Service == service) found = network;
            });
        return found;
    }
}

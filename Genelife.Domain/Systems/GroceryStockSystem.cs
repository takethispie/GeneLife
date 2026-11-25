using Arch.Core;
using Genelife.Consumption;
using Genelife.Economy;

namespace Genelife.Systems;

/// <summary>
/// Daily store restock and spoilage. Restock is funded through a company or the
/// government and ledgered; spoilage destroys goods but never money.
/// </summary>
public sealed class GroceryStockSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly GroceryStoreService stores;
    private readonly int interval;

    public GroceryStockSystem(
        World world,
        EconomyContext economy,
        Func<Entity, Components.Gen.NetworkId> allocateNetworkId,
        GroceryOptions? options = null,
        int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        stores = new GroceryStoreService(world, economy, allocateNetworkId, options);
        this.interval = interval ?? Math.Max(5, economy.Clock.TicksPerMonth / 4);
    }

    public string Name => "GroceryStock";
    public int Order => 476;
    public Cadence Cadence => new(Math.Max(1, interval));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        stores.Restock(tick, economy.Clock.MonthOfYear(tick));
        stores.Spoil(tick);
    }
}

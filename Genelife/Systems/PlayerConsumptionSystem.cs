using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Gen;
using Genelife.Components.Player;
using Genelife.Economy;
namespace Genelife.Systems;

/// <summary>
/// Projects household pantry supplies into the per-sim consumption flags used
/// by <see cref="ActionSystem"/>, so a player who has bought food can eat the
/// same way an NPC household can.
/// </summary>
public sealed class PlayerConsumptionSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly int interval;

    public PlayerConsumptionSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? economy.Clock.TicksPerMonth;
    }

    public string Name => "PlayerConsumption";
    public int Order => 420;
    public Cadence Cadence => new(Math.Max(1, interval));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var query = new QueryDescription()
            .WithAll<PlayerControlled>()
            .WithNone<Dead>();

        world.Query(in query, (Entity sim, ref PlayerControlled _) =>
        {
            var household = LifeSystemHelpers.HouseholdOf(world, economy, sim);
            if (household == Entity.Null || !world.Has<HouseholdPantry>(household))
                return;

            var pantry = world.Get<HouseholdPantry>(household);
            var state = world.Has<ConsumptionState>(sim)
                ? world.Get<ConsumptionState>(sim)
                : new ConsumptionState();

            if (pantry.FoodUnits > 0)
                state.FoodPurchased = true;
            if (pantry.HouseholdGoods > 0)
                state.HygienePurchased = true;

            if (world.Has<ConsumptionState>(sim))
                world.Get<ConsumptionState>(sim) = state;
            else
                world.Add(sim, state);
        });
    }
}

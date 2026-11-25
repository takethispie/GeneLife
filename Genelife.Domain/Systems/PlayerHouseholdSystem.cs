using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Housing;
using Genelife.Components.Player;
using Genelife.Economy;

namespace Genelife.Systems;

/// <summary>
/// Monthly player lease reconciliation. Eviction, family merges and housing
/// moves may change the household unit outside the player action path; the
/// lease view follows the authoritative household state.
/// </summary>
public sealed class PlayerHouseholdSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly int interval;

    public PlayerHouseholdSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? economy.Clock.TicksPerMonth;
    }

    public string Name => "PlayerHousehold";
    public int Order => 415;
    public Cadence Cadence => new(Math.Max(1, interval));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var query = new QueryDescription().WithAll<PlayerHousing>();
        world.Query(in query, (Entity sim, ref PlayerHousing lease) =>
        {
            var household = lease.Household;
            var unit = household != Entity.Null && world.IsAlive(household) && world.Has<Household>(household)
                ? world.Get<Household>(household).Unit
                : Entity.Null;

            if (household != Entity.Null && !world.IsAlive(household))
                household = Entity.Null;

            lease.Household = household;
            lease.Unit = unit;
            lease.LeaseActive = household != Entity.Null && unit != Entity.Null;
            world.Get<PlayerHousing>(sim) = lease;

            if (household != Entity.Null && world.IsAlive(household) && !world.Has<HouseholdPantry>(household))
                world.Add(household, new HouseholdPantry());
        });
    }
}

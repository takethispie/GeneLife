using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Systems;

/// <summary>
/// Monthly health drift from age, housing, unmet needs, poverty and care. Health
/// is the bridge between the lifecycle and mortality.
/// </summary>
public class HealthSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly QueryDescription query;
    private readonly int interval;

    public HealthSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? economy.Clock.TicksPerMonth;
        query = new QueryDescription().WithAll<Alive, Lifecycle, Health>().WithNone<Dead>();
    }

    public string Name => "Health";
    public int Order => 95;
    public Cadence Cadence => new(Math.Max(1, interval));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var missingHealth = new List<Entity>();
        var events = new List<object>();

        world.Query(in query, (Entity entity, ref Alive alive, ref Lifecycle lifecycle, ref Health health) =>
        {
            var drift = BaseDrift(alive.Age);
            if (world.Has<NeedsModifier>(entity))
                drift += world.Get<NeedsModifier>(entity).HealthDrift;

            if (world.Has<Needs>(entity))
            {
                var needs = world.Get<Needs>(entity);
                var lowest = Math.Min(Math.Min(needs.Hunger, needs.Energy), Math.Min(needs.Hygiene, needs.Bladder));
                if (lowest < 20f)
                    drift -= 0.5f;
            }

            var household = LifeSystemHelpers.HouseholdOf(world, economy, entity);
            if (household != Entity.Null && IsPoor(household))
                drift -= 0.3f;

            var care = false;
            if (world.Has<ConsumptionState>(entity) && world.Get<ConsumptionState>(entity).HealthPurchased)
            {
                drift += 1.5f;
                care = true;
                health.LastCareTick = tick;
            }

            var before = health.Value;
            health.Value = Math.Clamp(health.Value + drift, 0f, 100f);
            health.DeclineRate = -drift;
            world.Get<Health>(entity) = health;

            if (care)
            {
                events.Add(new MedicalCarePurchased(LifeSystemHelpers.NetworkIdOf(world, entity), 0, tick));
                events.Add(new HealthRestored(LifeSystemHelpers.NetworkIdOf(world, entity), health.Value, tick));
            }
            else if (drift < 0f && health.Value < before)
                events.Add(new HealthDeclined(LifeSystemHelpers.NetworkIdOf(world, entity), health.Value, -drift, tick));

            if (before >= 30f && health.Value < 30f)
                events.Add(new CareNeeded(LifeSystemHelpers.NetworkIdOf(world, entity), health.Value, tick));
        });

        world.Query(in new QueryDescription().WithAll<Alive, Lifecycle>().WithNone<Health, Dead>(),
            (Entity entity, ref Alive alive, ref Lifecycle _) => missingHealth.Add(entity));

        foreach (var entity in missingHealth)
        {
            if (!world.IsAlive(entity) || world.Has<Health>(entity))
                continue;
            world.Add(entity, new Health(InitialForAge(world.Get<Alive>(entity).Age)));
        }

        foreach (var @event in events)
            economy.Emit(@event);
    }

    private bool IsPoor(Entity household)
    {
        var finance = new Housing.HouseholdFinanceService(world, economy);
        var cash = finance.Cash(household);
        var rent = world.Has<Household>(household) ? world.Get<Household>(household).MonthlyRent : Money.Zero;
        return cash < rent + economy.Options.MonthlyConsumptionBudget;
    }

    private static float BaseDrift(int age)
    {
        var drift = 0f;
        if (age > 45)
            drift -= (age - 45) * 0.004f;
        if (age > 65)
            drift -= (age - 65) * 0.02f;
        if (age > 80)
            drift -= (age - 80) * 0.15f;
        return drift;
    }

    public static float InitialForAge(int age)
    {
        var value = 100f;
        if (age > 45)
            value -= (age - 45) * 0.3f;
        if (age > 65)
            value -= (age - 65) * 0.8f;
        if (age > 80)
            value -= (age - 80) * 1.2f;
        return Math.Clamp(value, 25f, 100f);
    }
}

using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;

namespace Genelife.Systems;

/// <summary>
/// Stochastic mortality from age, health, poverty and housing. Deaths are queued,
/// never applied inside the query.
/// </summary>
public class MortalitySystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly QueryDescription query;
    private readonly int interval;

    public MortalitySystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? 600;
        query = new QueryDescription().WithAll<Alive, Lifecycle, Health>().WithNone<Dead>();
    }

    public string Name => "Mortality";
    public int Order => 570;
    public Cadence Cadence => new(Math.Max(1, interval));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var deaths = new List<(Entity Entity, DeathCause Cause)>();

        world.Query(in query, (Entity entity, ref Alive alive, ref Lifecycle lifecycle, ref Health health) =>
        {
            var cause = CauseFor(entity, alive, health, tick);
            if (cause is null)
                return;
            deaths.Add((entity, cause.Value));
        });

        foreach (var (entity, cause) in deaths)
        {
            if (!world.IsAlive(entity) || world.Has<Dead>(entity))
                continue;

            world.Add(entity, new Dead(tick, cause));
            economy.Life.DeathQueue.Add(entity);
            economy.Life.DeathsThisYear++;
        }
    }

    private DeathCause? CauseFor(Entity entity, Alive alive, Health health, long tick)
    {
        if (health.Value <= 0f)
            return alive.Age >= 65 ? DeathCause.Age : DeathCause.Illness;
        if (alive.Age >= economy.Life.Policy.MaxAge)
            return DeathCause.Age;

        var ageRisk = alive.Age < 65
            ? 0f
            : (float)Math.Pow((alive.Age - 65) / 45.0, 2) * 0.02f;
        var healthRisk = (1f - health.Value / 100f) * 0.01f;

        var poor = IsPoor(entity);
        var povertyRisk = poor ? 0.004f : 0f;
        var housingRisk = 0f;
        if (world.Has<NeedsModifier>(entity))
        {
            var modifier = world.Get<NeedsModifier>(entity);
            if (modifier.IsHomeless)
                housingRisk = 0.01f;
        }
        else if (!economy.Housing.HouseholdByGenos.ContainsKey(entity))
        {
            housingRisk = 0.01f;
        }

        if (world.Has<HouseholdMember>(entity) &&
            world.IsAlive(world.Get<HouseholdMember>(entity).Household) &&
            world.Has<Household>(world.Get<HouseholdMember>(entity).Household) &&
            world.Get<Household>(world.Get<HouseholdMember>(entity).Household).Tenure == Tenure.Sheltered)
            housingRisk = Math.Max(housingRisk, 0.005f);

        var risk = Math.Min(0.15f, ageRisk + healthRisk + povertyRisk + housingRisk);
        if (risk <= 0f)
            return null;

        var roll = economy.Life.Random
            .For($"mortality.{LifeSystemHelpers.NetworkIdOf(world, entity)}.{tick}")
            .NextDouble();
        if (roll >= risk)
            return null;

        if (health.Value < 25f)
            return DeathCause.Illness;
        if (poor)
            return DeathCause.Starvation;
        if (housingRisk > 0f && alive.Age >= 65)
            return DeathCause.Exposure;
        return alive.Age >= 65 ? DeathCause.Age : DeathCause.Other;
    }

    private bool IsPoor(Entity entity)
    {
        var household = LifeSystemHelpers.HouseholdOf(world, economy, entity);
        if (household == Entity.Null)
            return !world.Has<Wallet>(entity) || world.Get<Wallet>(entity).Balance.IsZero;

        var finance = new Housing.HouseholdFinanceService(world, economy);
        var rent = world.Has<Household>(household) ? world.Get<Household>(household).MonthlyRent : Values.Money.Zero;
        return finance.Cash(household) < rent + economy.Options.MonthlyConsumptionBudget;
    }
}

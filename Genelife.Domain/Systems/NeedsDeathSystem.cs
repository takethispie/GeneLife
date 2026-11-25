using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;

namespace Genelife.Systems;

/// <summary>
/// Death from sustained critical need depletion. Debounces so a genos eating or
/// waiting for a purchase is not killed, and requires no household funds.
/// </summary>
public class NeedsDeathSystem : ISimulationSystem
{
    private const int TicksToDie = 200;
    private const float CriticalNeedValue = 0f;

    private readonly World world;
    private readonly EconomyContext economy;
    private readonly QueryDescription query;
    private readonly List<Entity> criticalBuffer = new(256);
    private readonly List<Entity> recoveredBuffer = new(256);

    public NeedsDeathSystem(World world, EconomyContext economy)
    {
        this.world = world;
        this.economy = economy;
        query = new QueryDescription().WithAll<Alive, Needs>().WithNone<Dead>();
    }

    public string Name => "NeedsDeath";
    public int Order => 575;
    public Cadence Cadence => Cadence.EveryTick;

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var critical = criticalBuffer;
        var recovered = recoveredBuffer;
        critical.Clear();
        recovered.Clear();

        world.Query(in query, (Entity entity, ref Alive _, ref Needs needs) =>
        {
            if (needs.Hunger > CriticalNeedValue)
            {
                recovered.Add(entity);
                return;
            }

            if (world.Has<CurrentAction>(entity) && world.Get<CurrentAction>(entity).Type == ActionType.Eating)
            {
                recovered.Add(entity);
                return;
            }

            if (HasFunds(entity))
            {
                recovered.Add(entity);
                return;
            }

            critical.Add(entity);
        });

        foreach (var entity in recovered)
            economy.Life.CriticalSince.Remove(entity);

        foreach (var entity in critical)
        {
            if (!economy.Life.CriticalSince.TryGetValue(entity, out var since))
            {
                economy.Life.CriticalSince[entity] = tick;
                continue;
            }

            if (tick - since < TicksToDie || !world.IsAlive(entity) || world.Has<Dead>(entity))
                continue;

            world.Add(entity, new Dead(tick, DeathCause.Starvation));
            economy.Life.DeathQueue.Add(entity);
            economy.Life.DeathsThisYear++;
            economy.Life.CriticalSince.Remove(entity);
        }
    }

    private bool HasFunds(Entity entity)
    {
        var household = LifeSystemHelpers.HouseholdOf(world, economy, entity);
        if (household != Entity.Null)
        {
            var finance = new Housing.HouseholdFinanceService(world, economy);
            return finance.Cash(household).IsPositive;
        }

        return world.Has<Wallet>(entity) && world.Get<Wallet>(entity).Balance.IsPositive;
    }
}

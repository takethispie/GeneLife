using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Housing;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Application.Systems;

/// <summary>
/// Graded arrears ladder: warning, late, formal notice with a cure window, then
/// eviction. Evictions are rate-limited and always try relocation before
/// homelessness. V1 omits the "move in with relatives" branch because family
/// links are not modelled yet; shelter is the fallback before the street.
/// </summary>
public class EvictionSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly HousingRegistry registry;
    private readonly HouseholdFinanceService finance;
    private readonly HousingAllocator allocator;
    private readonly int interval;

    public EvictionSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        registry = economy.Housing;
        finance = new HouseholdFinanceService(world, economy);
        allocator = new HousingAllocator(world, economy);
        this.interval = interval ?? Math.Max(1, economy.Clock.TicksPerMonth);
    }

    public string Name => "Eviction";
    public int Order => 516;
    public Cadence Cadence => new(interval);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var households = new List<(Entity Entity, Household Household)>();
        var query = new QueryDescription().WithAll<Household>();
        world.Query(in query, (Entity entity, ref Household household) => households.Add((entity, household)));
        households.Sort((a, b) => a.Entity.Id.CompareTo(b.Entity.Id));

        var evictions = 0;
        foreach (var (entity, household) in households)
        {
            if (!world.IsAlive(entity))
                continue;

            var data = world.Get<Household>(entity);
            if (data.Unit == Entity.Null || !world.IsAlive(data.Unit) || !world.Has<HousingUnit>(data.Unit))
                continue;

            var unit = world.Get<HousingUnit>(data.Unit);
            if (unit.IsCondemned)
            {
                if (evictions < economy.HousingOptions.MaxEvictionsPerCycle)
                {
                    Evict(tick, entity, data, condemned: true);
                    evictions++;
                }
                continue;
            }

            if (data.Tenure is not (Tenure.Renter or Tenure.Sheltered))
                continue;
            if (data.RentArrears.IsZero)
            {
                if (data.NoticeStage != 0)
                {
                    data.NoticeStage = 0;
                    data.NoticeStartedTick = 0;
                    data.CureDeadlineTick = 0;
                    world.Get<Household>(entity) = data;
                }
                continue;
            }

            if (data.ArrearsCycles >= 1 && data.NoticeStage < 1)
            {
                data.NoticeStage = 1;
                world.Get<Household>(entity) = data;
            }

            if (data.ArrearsCycles >= 2 && data.NoticeStage < 2)
            {
                data.NoticeStage = 2;
                world.Get<Household>(entity) = data;
            }

            if (data.ArrearsCycles >= 3 && data.NoticeStage < 3)
            {
                data.NoticeStage = 3;
                data.NoticeStartedTick = tick;
                data.CureDeadlineTick = tick + economy.Clock.TicksPerMonth * economy.HousingOptions.NoticeMonths;
                world.Get<Household>(entity) = data;
                economy.Emit(new TenantNoticeIssued(
                    NetworkIdOf(entity),
                    NetworkIdOf(data.Unit),
                    data.NoticeStage,
                    data.CureDeadlineTick,
                    tick));
            }

            if (data.NoticeStage >= 3 && tick >= data.CureDeadlineTick &&
                evictions < economy.HousingOptions.MaxEvictionsPerCycle)
            {
                Evict(tick, entity, data, condemned: false);
                evictions++;
            }
        }
    }

    public bool EvictNow(Entity household, long tick)
    {
        if (!world.IsAlive(household) || !world.Has<Household>(household))
            return false;

        Evict(tick, household, world.Get<Household>(household), condemned: false);
        return true;
    }

    private void Evict(long tick, Entity household, Household data, bool condemned)
    {
        var oldUnit = data.Unit;
        var oldArrears = data.RentArrears;
        var oldCycles = data.ArrearsCycles;

        registry.MoveOut(oldUnit, household);

        finance.TryDebit(
            household,
            HousingCostPolicy.MovingFee,
            LedgerKind.MovingFee,
            tick,
            EconomyAccountIds.Government,
            "housing.moving",
            out var movingShortfall);
        var movingFee = HousingCostPolicy.MovingFee - movingShortfall;
        economy.Government.Balance += movingFee;
        var debt = movingShortfall;

        data = world.Get<Household>(household);
        data.RentArrears = Money.Zero;
        data.UtilityArrears = Money.Zero;
        data.ArrearsCycles = 0;
        data.NoticeStage = 0;
        data.NoticeStartedTick = 0;
        data.CureDeadlineTick = 0;
        data.OverburdenMonths = 0;
        data.Unit = Entity.Null;
        data.Debt += debt;
        world.Get<Household>(household) = data;

        var relocated = allocator.TryFindBest(household, out var newUnit, out var sheltered, excludeUnit: oldUnit);
        if (relocated)
        {
            allocator.TryAssign(
                household,
                newUnit,
                sheltered ? Tenure.Sheltered : Tenure.Renter,
                tick,
                reference: "housing.evict-relocate");
        }
        else
        {
            economy.Emit(new HouseholdHomeless(NetworkIdOf(household), tick));
        }

        if (world.IsAlive(oldUnit) && world.Has<HousingUnit>(oldUnit))
        {
            var unit = world.Get<HousingUnit>(oldUnit);
            unit.Condition = Math.Clamp(unit.Condition - 0.05f, HousingCostPolicy.ConditionFloor, 1f);
            if (unit.Condition < HousingCostPolicy.UninhabitableThreshold)
                unit.IsCondemned = true;
            world.Get<HousingUnit>(oldUnit) = unit;
        }

        registry.Market.EvictionsThisYear++;
        economy.Emit(new TenantEvicted(
            NetworkIdOf(household),
            NetworkIdOf(oldUnit),
            oldArrears.Cents,
            oldCycles,
            tick));
    }

    private ulong NetworkIdOf(Entity entity) =>
        world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0UL;
}

using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Company;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Housing;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Systems;

/// <summary>
/// Owner-funded maintenance. Paid upkeep slows condition decay and repairs the
/// property; unpaid upkeep accelerates decay and can condemn units.
/// </summary>
public class MaintenanceSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly HousingRegistry registry;
    private readonly HouseholdFinanceService finance;
    private readonly int interval;

    public MaintenanceSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        registry = economy.Housing;
        finance = new HouseholdFinanceService(world, economy);
        this.interval = interval ?? Math.Max(1, economy.Clock.TicksPerMonth);
    }

    public string Name => "Maintenance";
    public int Order => 515;
    public Cadence Cadence => new(interval);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var properties = new List<(Entity Entity, Property Property)>();
        var query = new QueryDescription().WithAll<Property>();
        world.Query(in query, (Entity entity, ref Property property) => properties.Add((entity, property)));

        properties.Sort((a, b) => a.Entity.Id.CompareTo(b.Entity.Id));

        foreach (var entry in properties)
        {
            var entity = entry.Entity;
            var property = entry.Property;
            if (!world.IsAlive(entity))
                continue;

            var units = registry.UnitsByProperty.TryGetValue(entity, out var list) ? list : new List<Entity>();
            var due = HousingCostPolicy.MonthlyUpkeep(property, units.Count);
            var paid = PayUpkeep(tick, entity, property.Owner, due);

            var decay = HousingCostPolicy.ConditionDecayPerMonth;
            var factor = paid >= due ? 1f : 2f;
            var newCondition = property.Condition - decay * factor;
            if (paid >= due)
                newCondition += HousingCostPolicy.RepairPerMonth;

            property.Condition = Math.Clamp(
                newCondition,
                HousingCostPolicy.ConditionFloor,
                1f);
            world.Get<Property>(entity) = property;

            registry.Market.UpkeepPaidThisMonth += paid;
            registry.Market.UpkeepPaidThisYear += paid;

            if (paid >= due)
            {
                economy.Emit(new MaintenancePaid(
                    NetworkIdOf(entity),
                    NetworkIdOf(property.Owner),
                    paid.Cents,
                    property.Owner == Entity.Null,
                    tick));
            }
            else
            {
                economy.Emit(new MaintenanceSkipped(
                    NetworkIdOf(entity),
                    NetworkIdOf(property.Owner),
                    due.Cents,
                    paid.Cents,
                    tick));
            }

            var condemned = new List<Entity>();
            foreach (var unitEntity in units)
            {
                if (!world.IsAlive(unitEntity) || !world.Has<HousingUnit>(unitEntity))
                    continue;

                var unit = world.Get<HousingUnit>(unitEntity);
                var unitDelta = paid >= due
                    ? HousingCostPolicy.RepairPerMonth * 0.5f
                    : -decay * factor;
                unit.Condition = Math.Clamp(
                    unit.Condition + unitDelta,
                    HousingCostPolicy.ConditionFloor,
                    1f);

                if (!unit.IsCondemned && unit.Condition < HousingCostPolicy.UninhabitableThreshold)
                {
                    unit.IsCondemned = true;
                    economy.Emit(new UnitCondemned(
                        NetworkIdOf(entity),
                        NetworkIdOf(unitEntity),
                        unit.Condition,
                        tick));
                }

                world.Get<HousingUnit>(unitEntity) = unit;

                if (unit.IsCondemned && unit.IsVacant)
                    condemned.Add(unitEntity);
            }

            foreach (var unitEntity in condemned)
                registry.RemoveUnit(unitEntity);
        }
    }

    private Money PayUpkeep(long tick, Entity property, Entity owner, Money due)
    {
        if (!due.IsPositive)
            return Money.Zero;

        if (owner == Entity.Null)
        {
            var paid = economy.Government.Balance.Min(due);
            if (paid.IsPositive)
            {
                economy.Government.Balance -= paid;
                economy.Ledger.Record(
                    Guid.NewGuid(),
                    tick,
                    LedgerKind.MaintenancePaid,
                    EconomyAccountIds.Government,
                    null,
                    paid,
                    "housing.upkeep.municipal");
            }
            return paid;
        }

        if (world.IsAlive(owner) && world.Has<CompanyName>(owner))
        {
            var account = economy.Accounts.GetOrCreate(owner, Money.Zero, tick);
            var paid = account.Balance.Min(due);
            if (paid.IsPositive &&
                economy.Accounts.TryDebit(
                    owner,
                    paid,
                    LedgerKind.MaintenancePaid,
                    Guid.NewGuid(),
                    null,
                    "housing.upkeep",
                    tick))
            {
                return paid;
            }
            return Money.Zero;
        }

        if (world.IsAlive(owner) && world.Has<Household>(owner))
        {
            finance.TryDebit(
                owner,
                due,
                LedgerKind.MaintenancePaid,
                tick,
                null,
                "housing.upkeep",
                out var shortfall);
            return due - shortfall;
        }

        return Money.Zero;
    }

    private ulong NetworkIdOf(Entity entity) =>
        world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0UL;
}

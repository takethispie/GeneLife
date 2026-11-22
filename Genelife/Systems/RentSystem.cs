using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Company;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Housing;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Systems;

/// <summary>
/// Monthly rent and utility cycle. Runs after payroll so payday cash exists
/// before rent is due. Every payment is drawn from pooled household cash and
/// credited to the actual landlord or the municipality.
/// </summary>
public class RentSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly HousingRegistry registry;
    private readonly HouseholdFinanceService finance;
    private readonly int interval;

    public RentSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        registry = economy.Housing;
        finance = new HouseholdFinanceService(world, economy);
        this.interval = interval ?? Math.Max(1, economy.Clock.TicksPerMonth);
    }

    public string Name => "Rent";
    public int Order => 511;
    public Cadence Cadence => new(interval);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick, context.Time);

    public void Update(long tick) => Update(tick, economy.Clock);

    public void Update(long tick, SimulationClock clock)
    {
        registry.Market.ResetMonthly();

        var households = new List<(Entity Entity, Household Household)>();
        var query = new QueryDescription().WithAll<Household>();
        world.Query(in query, (Entity entity, ref Household household) => households.Add((entity, household)));

        households.Sort((a, b) => a.Entity.Id.CompareTo(b.Entity.Id));

        foreach (var (entity, household) in households)
        {
            if (!world.IsAlive(entity))
                continue;

            var data = world.Get<Household>(entity);
            if (data.Tenure == Tenure.Dependent)
                continue;
            if (data.LastRentTick == tick)
                continue;

            var unit = data.Unit;
            if (unit == Entity.Null || !world.IsAlive(unit) || !world.Has<HousingUnit>(unit))
            {
                data.LastRentTick = tick;
                data.LastUtilityTick = tick;
                world.Get<Household>(entity) = data;
                continue;
            }

            var unitData = world.Get<HousingUnit>(unit);
            var occupants = registry.MemberCount(entity);
            var utilityDue = HousingCostPolicy.UtilityBill(
                unitData.Tier,
                occupants,
                unitData.Capacity,
                clock.MonthOfYear(tick),
                unitData.Condition);
            var rentDue = data.Tenure == Tenure.Owner
                ? Money.Zero
                : data.Tenure == Tenure.Sheltered
                    ? HousingCostPolicy.ShelterFee(unitData.Tier)
                    : data.MonthlyRent;

            var landlord = registry.EffectiveOwner(unit);
            var landlordNetworkId = landlord == Entity.Null
                ? EconomyAccountIds.Government
                : NetworkIdOf(landlord);

            var rentPaid = PayRent(tick, entity, rentDue, landlord, landlordNetworkId, "rent.current");
            var utilityPaid = PayUtility(tick, entity, utilityDue);

            data = world.Get<Household>(entity);

            if (rentDue.IsPositive)
            {
                if (rentPaid >= rentDue)
                {
                    if (data.RentArrears.IsPositive)
                        data.RentArrears = ClearArrears(tick, entity, data.RentArrears, landlord, landlordNetworkId);
                    if (data.RentArrears.IsZero && data.ArrearsCycles > 0)
                    {
                        data.ArrearsCycles = 0;
                        data.NoticeStage = 0;
                        data.NoticeStartedTick = 0;
                        data.CureDeadlineTick = 0;
                        economy.Emit(new RentArrearsCleared(NetworkIdOf(entity), tick));
                    }
                }
                else
                {
                    var missed = rentDue - rentPaid;
                    data.RentArrears += missed;
                    if (rentPaid.IsZero)
                        data.ArrearsCycles++;
                    if (data.ArrearsCycles == 2)
                    {
                        var fee = HousingCostPolicy.LateFee(data.MonthlyRent);
                        data.RentArrears += fee;
                        economy.Emit(new LateFeeApplied(NetworkIdOf(entity), fee.Cents, data.ArrearsCycles, tick));
                    }
                    economy.Emit(new RentMissed(
                        NetworkIdOf(entity),
                        NetworkIdOf(unit),
                        rentDue.Cents,
                        rentPaid.Cents,
                        data.ArrearsCycles,
                        tick));
                }
            }

            if (utilityDue.IsPositive)
            {
                if (utilityPaid >= utilityDue)
                {
                    if (data.UtilityArrears.IsPositive)
                        data.UtilityArrears = ClearUtilityArrears(tick, entity, data.UtilityArrears);
                    data.NoUtilities = data.UtilityArrears.IsPositive;
                    economy.Emit(new UtilityBillPaid(NetworkIdOf(entity), NetworkIdOf(unit), utilityPaid.Cents, tick));
                }
                else
                {
                    var missed = utilityDue - utilityPaid;
                    data.UtilityArrears = (data.UtilityArrears + missed).Min(utilityDue * 2);
                    data.NoUtilities = true;
                    economy.Emit(new UtilityArrearsRecorded(
                        NetworkIdOf(entity),
                        utilityDue.Cents,
                        utilityPaid.Cents,
                        data.UtilityArrears.Cents,
                        tick));
                }
            }

            data.LastRentTick = tick;
            data.LastUtilityTick = tick;
            data.MemberCount = occupants;
            world.Get<Household>(entity) = data;
        }

    }

    private Money PayRent(
        long tick,
        Entity household,
        Money due,
        Entity landlord,
        ulong landlordNetworkId,
        string reference)
    {
        if (!due.IsPositive)
            return Money.Zero;

        finance.TryDebit(household, due, LedgerKind.RentPaid, tick, landlordNetworkId, reference, out var shortfall);
        var paid = due - shortfall;
        if (paid.IsPositive)
        {
            CreditLandlord(landlord, paid, tick);
            registry.Market.RentCollectedThisMonth += paid;
            registry.Market.RentCollectedThisYear += paid;
            economy.Emit(new RentPaid(
                NetworkIdOf(household),
                NetworkIdOf(registry.UnitOf(household)),
                landlordNetworkId,
                paid.Cents,
                tick));
        }
        return paid;
    }

    private Money PayUtility(long tick, Entity household, Money due)
    {
        if (!due.IsPositive)
            return Money.Zero;

        finance.TryDebit(
            household,
            due,
            LedgerKind.UtilityPaid,
            tick,
            EconomyAccountIds.Government,
            "rent.utility",
            out var shortfall);
        var paid = due - shortfall;
        economy.Government.Balance += paid;
        registry.Market.UtilityCollectedThisMonth += paid;
        registry.Market.UtilityCollectedThisYear += paid;
        return paid;
    }

    private Money ClearArrears(
        long tick,
        Entity household,
        Money arrears,
        Entity landlord,
        ulong landlordNetworkId)
    {
        finance.TryDebit(
            household,
            arrears,
            LedgerKind.RentArrears,
            tick,
            landlordNetworkId,
            "rent.arrears",
            out var shortfall);
        var paid = arrears - shortfall;
        if (paid.IsPositive)
        {
            CreditLandlord(landlord, paid, tick);
            registry.Market.RentCollectedThisMonth += paid;
            registry.Market.RentCollectedThisYear += paid;
        }
        return shortfall;
    }

    private Money ClearUtilityArrears(long tick, Entity household, Money arrears)
    {
        finance.TryDebit(
            household,
            arrears,
            LedgerKind.UtilityPaid,
            tick,
            EconomyAccountIds.Government,
            "rent.utility-arrears",
            out var shortfall);
        var paid = arrears - shortfall;
        economy.Government.Balance += paid;
        registry.Market.UtilityCollectedThisMonth += paid;
        registry.Market.UtilityCollectedThisYear += paid;
        return shortfall;
    }

    private void CreditLandlord(Entity landlord, Money amount, long tick)
    {
        if (!amount.IsPositive)
            return;

        if (landlord == Entity.Null)
        {
            economy.Government.Balance += amount;
            return;
        }

        if (world.IsAlive(landlord) && world.Has<CompanyName>(landlord))
        {
            economy.Accounts.ApplySettlement(landlord, amount, tick, revenue: true);
            return;
        }

        if (world.IsAlive(landlord) && world.Has<Household>(landlord))
        {
            var head = finance.Head(landlord);
            if (head != Entity.Null && world.IsAlive(head))
            {
                if (!world.Has<Wallet>(head))
                    world.Add(head, new Wallet(Money.Zero));
                ref var wallet = ref world.Get<Wallet>(head);
                wallet.Balance += amount;
            }
            return;
        }

        economy.Government.Balance += amount;
    }

    private ulong NetworkIdOf(Entity entity) =>
        world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0UL;
}

using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Employment;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Values;

namespace Genelife.Housing;

/// <summary>
/// Household cash pooling. The head pays first, then other adults by descending
/// balance, then adult savings. Child wallets are never touched.
/// </summary>
public sealed class HouseholdFinanceService
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly HousingRegistry registry;

    public HouseholdFinanceService(World world, EconomyContext economy)
    {
        this.world = world;
        this.economy = economy;
        registry = economy.Housing;
    }

    public IReadOnlyList<Entity> AdultMembers(Entity household)
    {
        var members = new List<Entity>();
        var head = Head(household);

        foreach (var member in registry.GetMembers(household))
        {
            if (world.IsAlive(member) && IsAdult(member))
                members.Add(member);
        }

        if (members.Count == 0 && head != Entity.Null && world.IsAlive(head) && IsAdult(head))
            members.Add(head);

        members.Sort((a, b) => ComparePaymentOrder(household, a, b));
        return members;
    }

    public int Earners(Entity household)
    {
        var count = 0;
        foreach (var member in AdultMembers(household))
        {
            if (world.Has<Employee>(member))
                count++;
        }
        return count;
    }

    public Money Cash(Entity household)
    {
        var cash = Money.Zero;
        foreach (var member in AdultMembers(household))
        {
            if (world.Has<Wallet>(member))
                cash += world.Get<Wallet>(member).Balance;
        }
        return cash;
    }

    public Money Savings(Entity household)
    {
        var savings = Money.Zero;
        foreach (var member in AdultMembers(household))
        {
            if (world.Has<SavingsAccount>(member))
                savings += world.Get<SavingsAccount>(member).Balance;
        }
        return savings;
    }

    public Money TotalWealth(Entity household) => Cash(household) + Savings(household);

    /// <summary>
    /// Monthly income estimate for affordability checks: paid work converted to
    /// a month, plus the universal basic income floor every adult receives.
    /// </summary>
    public Money MonthlyIncome(Entity household)
    {
        var monthsPerPayCycle = Math.Max(1, economy.Clock.TicksPerPayCycle / economy.Clock.TicksPerMonth);
        var income = Money.Zero;
        foreach (var member in AdultMembers(household))
        {
            if (world.Has<Employee>(member))
                income += new Money(world.Get<Employee>(member).SalaryPerCycle.Cents / monthsPerPayCycle);
            income += economy.Options.UniversalBasicIncome;
        }
        return income;
    }

    public bool TryDebit(
        Entity household,
        Money amount,
        LedgerKind kind,
        long tick,
        ulong? toNetworkId,
        string reference,
        out Money shortfall)
    {
        shortfall = amount;
        if (!amount.IsPositive)
        {
            shortfall = Money.Zero;
            return true;
        }

        var adults = AdultMembers(household);
        var remaining = amount;

        foreach (var member in adults)
        {
            if (!remaining.IsPositive)
                break;
            if (!world.Has<Wallet>(member))
                continue;

            ref var wallet = ref world.Get<Wallet>(member);
            var take = wallet.Balance.Min(remaining);
            if (!take.IsPositive)
                continue;

            wallet.Balance -= take;
            remaining -= take;
            RecordDebit(member, take, kind, tick, toNetworkId, reference);
        }

        if (remaining.IsPositive)
        {
            foreach (var member in adults)
            {
                if (!remaining.IsPositive)
                    break;
                if (!world.Has<SavingsAccount>(member))
                    continue;

                ref var savings = ref world.Get<SavingsAccount>(member);
                var take = savings.Balance.Min(remaining);
                if (!take.IsPositive)
                    continue;

                savings.Balance -= take;
                savings.LifetimeWithdrawn += take;
                remaining -= take;
                RecordDebit(member, take, kind, tick, toNetworkId, reference);
            }
        }

        shortfall = remaining;
        return !remaining.IsPositive;
    }

    public void Credit(Entity sim, Money amount, LedgerKind kind, long tick, ulong? fromNetworkId, string reference)
    {
        if (!amount.IsPositive || !world.IsAlive(sim))
            return;

        if (!world.Has<Wallet>(sim))
            world.Add(sim, new Wallet(Money.Zero));

        ref var wallet = ref world.Get<Wallet>(sim);
        wallet.Balance += amount;

        economy.Ledger.Record(
            Guid.NewGuid(),
            tick,
            kind,
            fromNetworkId,
            CompanyAccountService.NetworkIdOf(world, sim),
            amount,
            reference);
    }

    public void CreditHousehold(
        Entity household,
        Money amount,
        LedgerKind kind,
        long tick,
        ulong? fromNetworkId,
        string reference)
    {
        var head = Head(household);
        if (head != Entity.Null)
            Credit(head, amount, kind, tick, fromNetworkId, reference);
    }

    public Entity Head(Entity household)
    {
        if (world.IsAlive(household) && world.Has<Household>(household))
        {
            var data = world.Get<Household>(household);
            if (data.Head != Entity.Null)
                return data.Head;
        }

        foreach (var member in registry.GetMembers(household))
        {
            if (world.IsAlive(member) && world.Has<HouseholdMember>(member) &&
                world.Get<HouseholdMember>(member).Role == HouseholdRole.Head)
                return member;
        }

        return Entity.Null;
    }

    public bool IsAdult(Entity sim)
    {
        if (!world.IsAlive(sim))
            return false;
        return !world.Has<Alive>(sim) || world.Get<Alive>(sim).Age >= 18;
    }

    private int ComparePaymentOrder(Entity household, Entity a, Entity b)
    {
        var head = Head(household);
        if (a == head && b != head) return -1;
        if (b == head && a != head) return 1;

        var aBalance = world.Has<Wallet>(a) ? world.Get<Wallet>(a).Balance : Money.Zero;
        var bBalance = world.Has<Wallet>(b) ? world.Get<Wallet>(b).Balance : Money.Zero;
        var byBalance = bBalance.Cents.CompareTo(aBalance.Cents);
        if (byBalance != 0)
            return byBalance;

        return CompanyAccountService.NetworkIdOf(world, a)
            .CompareTo(CompanyAccountService.NetworkIdOf(world, b));
    }

    private void RecordDebit(
        Entity sim,
        Money amount,
        LedgerKind kind,
        long tick,
        ulong? toNetworkId,
        string reference)
    {
        economy.Ledger.Record(
            Guid.NewGuid(),
            tick,
            kind,
            CompanyAccountService.NetworkIdOf(world, sim),
            toNetworkId,
            amount,
            reference);
    }
}

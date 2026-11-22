using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Company;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Values;

namespace Genelife.Economy;

/// <summary>
/// Single mutation point for company cash. Every debit/credit also writes a
/// ledger entry, so an account balance can always be reconstructed.
/// </summary>
public sealed class CompanyAccountService
{
    private readonly World world;
    private readonly FinanceLedger ledger;

    public CompanyAccountService(World world, FinanceLedger ledger)
    {
        this.world = world;
        this.ledger = ledger;
    }

    public bool TryGet(Entity company, out CompanyAccount account)
    {
        if (world.IsAlive(company) && world.Has<CompanyAccount>(company))
        {
            account = world.Get<CompanyAccount>(company);
            return true;
        }

        account = default;
        return false;
    }

    public CompanyAccount GetOrCreate(Entity company, Money initial, long tick, string reference = "account.seed")
    {
        if (TryGet(company, out var account))
            return account;

        account = CompanyAccount.Create(initial, tick);
        world.Add(company, account);

        if (initial.IsPositive)
        {
            ledger.Record(
                Guid.NewGuid(),
                tick,
                LedgerKind.CapitalInjected,
                null,
                NetworkIdOf(world, company),
                initial,
                reference);
        }

        return account;
    }

    public void Set(Entity company, in CompanyAccount account)
    {
        if (world.IsAlive(company) && world.Has<CompanyAccount>(company))
            world.Get<CompanyAccount>(company) = account;
        else if (world.IsAlive(company))
            world.Add(company, account);
    }

    public bool TryDebit(
        Entity company,
        Money amount,
        LedgerKind kind,
        Guid transactionId,
        ulong? toNetworkId,
        string reference,
        long tick,
        string? memo = null)
    {
        if (amount.IsNegative)
            throw new ArgumentOutOfRangeException(nameof(amount), "Debit amount cannot be negative.");
        if (amount.IsZero)
            return true;
        if (ledger.Contains(transactionId))
            return true;
        if (!TryGet(company, out var account) || account.Balance < amount)
            return false;

        account.Balance -= amount;
        Set(company, account);
        ledger.Record(
            transactionId,
            tick,
            kind,
            NetworkIdOf(world, company),
            toNetworkId,
            amount,
            reference,
            memo);
        return true;
    }

    public bool Credit(
        Entity company,
        Money amount,
        LedgerKind kind,
        Guid transactionId,
        ulong? fromNetworkId,
        string reference,
        long tick,
        string? memo = null)
    {
        if (amount.IsNegative)
            throw new ArgumentOutOfRangeException(nameof(amount), "Credit amount cannot be negative.");
        if (amount.IsZero)
            return true;
        if (ledger.Contains(transactionId))
            return true;

        var account = GetOrCreate(company, Money.Zero, tick);
        account.Balance += amount;
        Set(company, account);
        ledger.Record(
            transactionId,
            tick,
            kind,
            fromNetworkId,
            NetworkIdOf(world, company),
            amount,
            reference,
            memo);
        return true;
    }

    /// <summary>
    /// Credits a company for a payment whose ledger leg was already written by
    /// the payer (household finance, grant or contract settlement). Use this
    /// instead of mutating CompanyAccount directly; the ledger is not duplicated.
    /// </summary>
    public void ApplySettlement(Entity company, Money amount, long tick, bool revenue = false)
    {
        if (!amount.IsPositive || !world.IsAlive(company))
            return;

        var account = GetOrCreate(company, Money.Zero, tick);
        account.Balance += amount;
        if (revenue)
            account.RevenueLastCycle += amount;
        Set(company, account);
    }

    public bool TryTransfer(
        Entity from,
        Entity to,
        Money amount,
        LedgerKind kind,
        Guid transactionId,
        string reference,
        long tick,
        string? memo = null)
    {
        if (from == to || amount.IsNegative)
            return false;
        if (ledger.Contains(transactionId))
            return true;

        if (!TryGet(from, out var fromAccount) || fromAccount.Balance < amount)
            return false;

        fromAccount.Balance -= amount;
        Set(from, fromAccount);

        var toAccount = GetOrCreate(to, Money.Zero, tick);
        toAccount.Balance += amount;
        Set(to, toAccount);

        ledger.Record(
            transactionId,
            tick,
            kind,
            NetworkIdOf(world, from),
            NetworkIdOf(world, to),
            amount,
            reference,
            memo);
        return true;
    }

    public static ulong NetworkIdOf(World world, Entity entity)
    {
        return world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0UL;
    }

    public ulong NetworkIdOfEntity(Entity entity) => NetworkIdOf(world, entity);
}

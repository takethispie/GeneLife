using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Family;
using Genelife.Consumption;
using Genelife.Housing;
using Genelife.Life;
using Genelife.Values;

namespace Genelife.Economy;

public sealed class PendingCompanyRevenue
{
    public Money Total { get; set; }
    public Dictionary<string, Money> ByCategory { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<PendingSpend> Spends { get; } = new();

    public void Add(string category, ulong genosNetworkId, Money amount, long tick)
    {
        Total += amount;
        ByCategory.TryGetValue(category, out var current);
        ByCategory[category] = current + amount;
        Spends.Add(new PendingSpend(genosNetworkId, category, amount, tick));
    }
}

public readonly record struct PendingSpend(
    ulong GenosNetworkId,
    string Category,
    Money Amount,
    long Tick);

/// <summary>
/// Shared economy services handed to every economy system. This is the economy
/// analogue of EmploymentIndex: a plain C# object owned by the simulation engine
/// and shared with systems and admin reads.
/// </summary>
public sealed class EconomyContext
{
    private readonly List<object> outboundEvents = new();

    public EconomyContext(
        World world,
        EmploymentIndex? employment = null,
        EconomyOptions? options = null,
        SimulationClock? clock = null,
        int randomSeed = 1,
        HousingOptions? housingOptions = null,
        LifeStagePolicy? lifePolicy = null,
        GeneticsOptions? geneticsOptions = null)
    {
        World = world;
        Employment = employment ?? new EmploymentIndex();
        Options = options ?? EconomyOptions.Default;
        Clock = clock ?? SimulationClock.Default;
        Random = new SimulationRandom(randomSeed);
        HousingOptions = (housingOptions ?? HousingOptions.Default).Clone();
        Genetics = geneticsOptions ?? GeneticsOptions.Default;
        Housing = new HousingRegistry(world);
        Life = new LifeContext(world, Clock, Random, lifePolicy, randomSeed: randomSeed);
        Ledger = new FinanceLedger();
        Accounts = new CompanyAccountService(world, Ledger);
        Government = new GovernmentAccount();
        Pension = new PensionFund();
        Bank = new Bank { Balance = Options.InitialBankCapital };
        Loans = new LoanBook();

        if (Bank.Balance.IsPositive)
        {
            Ledger.Record(
                FixedTransactionIds.BankCapital,
                0,
                LedgerKind.CapitalInjected,
                null,
                Bank.NetworkId,
                Bank.Balance,
                "bank.capital");
        }
    }

    public World World { get; }
    public EmploymentIndex Employment { get; }
    public UtilityOptions UtilityOptions { get; set; } = new();

    public EconomyOptions Options { get; }

    /// <summary>
    /// Nominal monthly consumption budget for the current simulation year
    /// (see <see cref="EconomyOptions.MonthlyConsumptionBudgetFor"/>). Single
    /// source of truth so the spending pass, the poverty checks and the unmet
    /// accounting all use the same indexed figure.
    /// </summary>
    public Money MonthlyConsumptionBudget => Options.MonthlyConsumptionBudgetFor(Clock.YearOf(Tick));
    public HousingOptions HousingOptions { get; }
    public GeneticsOptions Genetics { get; }
    public HousingRegistry Housing { get; }
    public LifeContext Life { get; }
    public SimulationClock Clock { get; }
    public SimulationRandom Random { get; }
    public FinanceLedger Ledger { get; }
    public CompanyAccountService Accounts { get; }
    public GovernmentAccount Government { get; }
    public PensionFund Pension { get; }
    public Bank Bank { get; }
    public LoanBook Loans { get; }
    public Dictionary<Entity, PendingCompanyRevenue> PendingRevenue { get; } = new();
    public List<EconomySnapshot> MetricHistory { get; } = new();
    public long Tick { get; set; }

    public Money WagesPaidThisYear { get; set; }
    public Money RevenueThisYear { get; set; }
    public Money TaxThisYear { get; set; }
    public Money BasicIncomeThisYear { get; set; }
    public Money ConsumptionThisYear { get; set; }

    /// <summary>
    /// Firms liquidated since the last yearly snapshot. This is the flow the
    /// company-failure-rate band measures, so it is reset with the other yearly
    /// counters rather than derived from the monotonic Bankrupt stock.
    /// </summary>
    public int CompaniesFailedThisYear { get; set; }

    public void Emit(object @event) => outboundEvents.Add(@event);

    public IReadOnlyList<object> PendingEvents => outboundEvents;

    public List<object> DrainEvents()
    {
        var drained = new List<object>(outboundEvents);
        outboundEvents.Clear();
        return drained;
    }

    public void BookRevenue(Entity company, string category, ulong genosNetworkId, Money amount, long tick)
    {
        if (!PendingRevenue.TryGetValue(company, out var pending))
        {
            pending = new PendingCompanyRevenue();
            PendingRevenue[company] = pending;
        }
        pending.Add(category, genosNetworkId, amount, tick);
    }

    public MoneySupplyBreakdown GetMoneySupplyBreakdown()
    {
        var householdCash = Money.Zero;
        var walletQuery = new QueryDescription().WithAll<Wallet>();
        World.Query(in walletQuery, (ref Wallet wallet) => householdCash += wallet.Balance);

        var householdSavings = Money.Zero;
        var savingsQuery = new QueryDescription().WithAll<SavingsAccount>();
        World.Query(in savingsQuery, (ref SavingsAccount savings) => householdSavings += savings.Balance);

        var companyCash = Money.Zero;
        var companyQuery = new QueryDescription().WithAll<CompanyAccount>();
        World.Query(in companyQuery, (ref CompanyAccount account) => companyCash += account.Balance);

        var pending = Money.Zero;
        foreach (var revenue in PendingRevenue.Values)
            pending += revenue.Total;

        var trusts = Money.Zero;
        var trustQuery = new QueryDescription().WithAll<Components.Finance.TrustAccount>();
        World.Query(in trustQuery, (ref Components.Finance.TrustAccount trust) => trusts += trust.Balance);

        return new MoneySupplyBreakdown(
            householdCash,
            householdSavings,
            companyCash,
            Government.Balance,
            Pension.Balance,
            Bank.Balance + Bank.InterestPool,
            pending,
            trusts);
    }

    public Money TotalMoneySupply() => GetMoneySupplyBreakdown().Total;

    public Money TotalArrears()
    {
        var total = Money.Zero;
        var query = new QueryDescription().WithAll<CompanyAccount>();
        World.Query(in query, (ref CompanyAccount account) => total += account.Arrears + account.TaxArrears);
        return total;
    }

    public void ResetYearCounters()
    {
        WagesPaidThisYear = Money.Zero;
        RevenueThisYear = Money.Zero;
        TaxThisYear = Money.Zero;
        BasicIncomeThisYear = Money.Zero;
        ConsumptionThisYear = Money.Zero;
        CompaniesFailedThisYear = 0;
    }

    public void Clear()
    {
        Ledger.Clear();
        Government.Clear();
        Pension.Clear();
        Bank.Clear();
        Bank.Balance = Options.InitialBankCapital;
        Loans.Clear();
        PendingRevenue.Clear();
        MetricHistory.Clear();
        Housing.Clear();
        Life.Clear();
        outboundEvents.Clear();
        Tick = 0;
        ResetYearCounters();
    }
}

public readonly record struct MoneySupplyBreakdown(
    Money HouseholdCash,
    Money HouseholdSavings,
    Money CompanyCash,
    Money Government,
    Money Pension,
    Money Bank,
    Money PendingRevenue,
    Money MinorTrusts = default)
{
    public Money Total =>
        HouseholdCash + HouseholdSavings + CompanyCash + Government + Pension + Bank +
        PendingRevenue + MinorTrusts;
}

public static class FixedTransactionIds
{
    public static readonly Guid BankCapital = new("11111111-1111-1111-1111-111111111111");
}

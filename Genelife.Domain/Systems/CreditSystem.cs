using Arch.Core;
using Genelife.Components.Company;
using Genelife.Components.Finance;
using Genelife.Economy;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Systems;

/// <summary>
/// Prices and services company credit. Principal comes from the bank's funded
/// balance; interest is routed into the bank interest pool so savings interest
/// never mints money. Missed payments move a loan to default.
/// </summary>
public class CreditSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly int interval;

    public CreditSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? economy.Clock.TicksPerPayCycle;
    }

    public string Name => "Credit";
    public int Order => 538;
    public Cadence Cadence => new(interval <= 0 ? 1 : interval);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        ServiceLoans(tick);
        AutoIssue(tick);
    }

    public Loan? IssueLoan(Entity company, Money amount, long tick)
    {
        if (!world.IsAlive(company) || !amount.IsPositive)
            return null;
        if (!economy.Accounts.TryGet(company, out var account))
            return null;
        if (account.State is CompanySolvency.Insolvent or CompanySolvency.Bankrupt)
            return null;
        if (economy.Loans.ActiveForCompany(company) is not null)
            return null;

        var bank = economy.Bank;
        if (bank.Balance < amount)
            return null;

        var totalDebt = economy.Loans.TotalOutstanding() + amount;
        var revenueLimit = account.RevenueLastCycle.Multiply(economy.Options.DebtToRevenueLimit);
        if (account.RevenueLastCycle.IsPositive && totalDebt > revenueLimit && totalDebt > economy.Options.MaximumLoan)
            return null;

        if (amount > economy.Options.MaximumLoan)
            amount = economy.Options.MaximumLoan;

        bank.Balance -= amount;
        economy.Accounts.Credit(
            company,
            amount,
            LedgerKind.LoanIssued,
            Guid.NewGuid(),
            EconomyAccountIds.Bank,
            "loan.issue",
            tick);

        var term = Math.Max(1, economy.Options.LoanTermCycles);
        var rate = economy.Options.LoanAnnualRate;
        var payment = amount.Multiply(1m / term + rate / 4m);

        var loan = new Loan
        {
            LoanId = economy.Loans.NextId(),
            Company = company,
            CompanyNetworkId = CompanyAccountService.NetworkIdOf(world, company),
            Principal = amount,
            Outstanding = amount,
            AnnualRate = rate,
            TermCycles = term,
            CyclesRemaining = term,
            PaymentPerCycle = payment,
            State = LoanState.Active,
            IssuedAtTick = tick
        };
        economy.Loans.Add(loan);

        economy.Emit(new LoanIssued(
            loan.LoanId,
            loan.CompanyNetworkId,
            amount.Cents,
            rate,
            payment.Cents,
            tick));
        return loan;
    }

    private void AutoIssue(long tick)
    {
        var companies = new List<(Entity Entity, CompanyAccount Account)>();
        var query = new QueryDescription().WithAll<CompanyName, CompanyAccount>();
        world.Query(in query, (Entity entity, ref CompanyName _, ref CompanyAccount account) =>
            companies.Add((entity, account)));

        foreach (var (company, account) in companies)
        {
            if (!world.IsAlive(company) || account.State != CompanySolvency.Tight)
                continue;
            if (!account.ReservedForPayroll.IsPositive || account.Balance >= account.ReservedForPayroll)
                continue;
            if (economy.Loans.ActiveForCompany(company) is not null)
                continue;

            var gap = account.ReservedForPayroll - account.Balance;
            var amount = gap.Max(Money.FromUnits(2_000m)).Min(economy.Options.MaximumLoan);
            IssueLoan(company, amount, tick);
        }
    }

    private void ServiceLoans(long tick)
    {
        var loans = economy.Loans.Loans.ToList();
        foreach (var loan in loans)
        {
            if (loan.State is LoanState.Repaid or LoanState.Defaulted)
                continue;

            if (!world.IsAlive(loan.Company))
            {
                loan.State = LoanState.Defaulted;
                economy.Emit(new LoanDefaulted(loan.LoanId, loan.CompanyNetworkId, tick));
                continue;
            }

            var interest = loan.Outstanding.Multiply(loan.AnnualRate / 4m);
            loan.Outstanding += interest;

            if (!economy.Accounts.TryGet(loan.Company, out var account) || account.Balance < loan.PaymentPerCycle)
            {
                loan.MissedPayments++;
                loan.State = LoanState.Delinquent;
                economy.Emit(new LoanPaymentMissed(
                    loan.LoanId,
                    loan.CompanyNetworkId,
                    loan.MissedPayments,
                    tick));

                if (loan.MissedPayments >= 3)
                {
                    loan.State = LoanState.Defaulted;
                    economy.Emit(new LoanDefaulted(loan.LoanId, loan.CompanyNetworkId, tick));
                }
                continue;
            }

            var payment = loan.PaymentPerCycle.Min(loan.Outstanding);
            var interestPaid = payment.Min(interest);
            var principalPaid = payment - interestPaid;

            if (interestPaid.IsPositive &&
                economy.Accounts.TryDebit(
                    loan.Company,
                    interestPaid,
                    LedgerKind.LoanInterest,
                    Guid.NewGuid(),
                    EconomyAccountIds.Bank,
                    "loan.interest",
                    tick))
            {
                economy.Bank.InterestPool += interestPaid;
            }

            if (principalPaid.IsPositive &&
                economy.Accounts.TryDebit(
                    loan.Company,
                    principalPaid,
                    LedgerKind.LoanPrincipal,
                    Guid.NewGuid(),
                    EconomyAccountIds.Bank,
                    "loan.principal",
                    tick))
            {
                economy.Bank.Balance += principalPaid;
            }

            var totalPaid = interestPaid + principalPaid;
            loan.Outstanding -= totalPaid;
            loan.CyclesRemaining--;
            loan.MissedPayments = 0;
            loan.State = loan.Outstanding.IsPositive ? LoanState.Active : LoanState.Repaid;

            if (account.State is CompanySolvency.Healthy or CompanySolvency.Tight)
            {
                account = economy.Accounts.GetOrCreate(loan.Company, Money.Zero, tick);
                account.InterestPaidThisCycle += interestPaid;
                economy.Accounts.Set(loan.Company, account);
            }

            economy.Emit(new LoanPaymentMade(
                loan.LoanId,
                loan.CompanyNetworkId,
                totalPaid.Cents,
                interestPaid.Cents,
                principalPaid.Cents,
                loan.Outstanding.Cents,
                tick));
        }
    }
}

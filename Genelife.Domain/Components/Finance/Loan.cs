using Arch.Core;
using Genelife.Values;

namespace Genelife.Components.Finance;

public enum LoanState
{
    Active,
    Delinquent,
    Defaulted,
    Repaid
}

/// <summary>
/// Company credit obligation. Interest is paid before principal; missed
/// payments move the loan through Delinquent to Defaulted.
/// </summary>
public sealed class Loan
{
    public ulong LoanId { get; set; }
    public Entity Company { get; set; }
    public ulong CompanyNetworkId { get; set; }
    public Money Principal { get; set; }
    public Money Outstanding { get; set; }
    public decimal AnnualRate { get; set; }
    public int TermCycles { get; set; }
    public int CyclesRemaining { get; set; }
    public Money PaymentPerCycle { get; set; }
    public LoanState State { get; set; }
    public int MissedPayments { get; set; }
    public long IssuedAtTick { get; set; }
}

/// <summary>
/// In-memory loan index shared by CreditSystem and admin reads.
/// </summary>
public sealed class LoanBook
{
    private readonly List<Loan> loans = new();
    private ulong nextId = 1;

    public IReadOnlyList<Loan> Loans => loans;

    public ulong NextId() => nextId++;

    public void Add(Loan loan)
    {
        if (loan.LoanId == 0)
            loan.LoanId = NextId();
        loans.Add(loan);
    }

    public IEnumerable<Loan> ForCompany(Entity company) => loans.Where(loan => loan.Company == company);

    public Loan? ActiveForCompany(Entity company) =>
        loans.FirstOrDefault(loan =>
            loan.Company == company &&
            loan.State is LoanState.Active or LoanState.Delinquent);

    public Money TotalOutstanding()
    {
        var total = Money.Zero;
        foreach (var loan in loans)
        {
            if (loan.State is LoanState.Active or LoanState.Delinquent)
                total += loan.Outstanding;
        }
        return total;
    }

    public void Clear() => loans.Clear();
}

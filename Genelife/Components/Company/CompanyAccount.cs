using Genelife.Values;

namespace Genelife.Components.Company;

public enum CompanySolvency
{
    Healthy,
    Tight,
    Arrears,
    Insolvent,
    Bankrupt
}

/// <summary>
/// Company cash, recurring obligations and solvency state. Every balance change
/// goes through CompanyAccountService so the ledger stays authoritative.
/// </summary>
public record struct CompanyAccount
{
    public Money Balance;
    public Money ReservedForPayroll;
    public Money RevenueLastCycle;
    public Money WagesLastCycle;
    public Money CostsLastCycle;
    public Money InterestPaidLastCycle;
    public Money Arrears;
    public Money TaxArrears;
    public Money LossCarryForward;
    public long LossCarryForwardExpiresAtTick;
    public Money LastProfit;
    public Money LastTaxPaid;
    public CompanySolvency State;
    public long StateChangedAtTick;
    public int ArrearsCycles;
    public int InsolventCycles;
    public bool HiringFrozen;

    public static CompanyAccount Create(Money initialBalance, long tick) => new()
    {
        Balance = initialBalance,
        State = initialBalance.IsPositive ? CompanySolvency.Healthy : CompanySolvency.Tight,
        StateChangedAtTick = tick
    };
}

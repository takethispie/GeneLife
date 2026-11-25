using Genelife.Values;

namespace Genelife.Components.Employment;

/// <summary>
/// Retiree income stream derived from the individual pension statement.
/// </summary>
public record struct RetirementIncome
{
    public Money MonthlyPension;
    public long StartedTick;
    public bool Reduced;
    public string FundReference;
    public bool MeansTested;
    public long LastPaidTick;

    public RetirementIncome(Money monthlyPension, long startedTick, string fundReference = "pension", bool meansTested = false)
    {
        MonthlyPension = monthlyPension;
        StartedTick = startedTick;
        Reduced = false;
        FundReference = fundReference;
        MeansTested = meansTested;
        LastPaidTick = 0;
    }
}

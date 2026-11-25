using Genelife.Components.Employment;
using Genelife.Values;

namespace Genelife.Life;

/// <summary>
/// Pension accrual, benefit formula and fund policy. Contribution movement is
/// owned by PayrollSystem; this policy turns statements into retirement income
/// and projected liabilities.
/// </summary>
public sealed class PensionPolicy
{
    public int MinimumContributingCycles { get; set; } = 8;
    public Money MinimumMonthlyPension { get; set; } = Money.FromUnits(40m);
    public decimal AnnuityAdjustment { get; set; } = 1.1m;
    public decimal AnnualFundYield { get; set; } = 0.02m;
    public decimal AnnualIndexation { get; set; } = 0.02m;

    public static PensionPolicy Default { get; } = new();

    public decimal ExpectedYearsInRetirement(LifeStagePolicy stagePolicy) =>
        Math.Max(5m, (stagePolicy.ElderlyAge - stagePolicy.RetirementAge) * AnnuityAdjustment);

    public Money ProjectedMonthlyPension(PensionAccount account, LifeStagePolicy stagePolicy)
    {
        if (account.ContributingCycles < MinimumContributingCycles)
            return Money.Zero;

        var years = ExpectedYearsInRetirement(stagePolicy);
        var annual = account.Total.Multiply(1m / years);
        var monthly = annual.Multiply(1m / 12m);
        return monthly.Max(MinimumMonthlyPension);
    }

    public (Money Amount, bool MeansTested) EntitlementFor(PensionAccount account, LifeStagePolicy stagePolicy)
    {
        var meansTested = account.ContributingCycles < MinimumContributingCycles;
        var amount = meansTested
            ? MinimumMonthlyPension
            : ProjectedMonthlyPension(account, stagePolicy);
        return (amount, meansTested);
    }

    public Money ProjectedLiability(PensionAccount account, LifeStagePolicy stagePolicy)
    {
        var monthly = ProjectedMonthlyPension(account, stagePolicy);
        if (!monthly.IsPositive)
            return Money.Zero;

        var monthsRemaining = Math.Max(12, (int)Math.Round(ExpectedYearsInRetirement(stagePolicy) * 12m));
        return monthly * monthsRemaining;
    }

    public Money IndexedAmount(Money amount, bool fundingSufficient) =>
        fundingSufficient ? amount.Multiply(1m + AnnualIndexation) : amount;
}

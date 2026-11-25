using Genelife.Values;

namespace Genelife.Components.Employment;

/// <summary>
/// Individual pension statement. The pooled PensionFund must equal the sum of
/// these accounts while no payouts have occurred.
/// </summary>
public record struct PensionAccount
{
    public Money EmployeeContributions;
    public Money EmployerContributions;
    public int ContributingCycles;
    public long LastContributionTick;

    public Money Total => EmployeeContributions + EmployerContributions;
}

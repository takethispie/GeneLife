using Genelife.Values;

namespace Genelife.Family;

public sealed class GuardianshipOptions
{
    public Money StateCareMonthlyCost { get; set; } = Money.FromUnits(100m);

    public static GuardianshipOptions Default { get; } = new();

    public void Validate()
    {
        if (StateCareMonthlyCost.IsNegative)
            throw new ArgumentOutOfRangeException(nameof(StateCareMonthlyCost), "StateCareMonthlyCost must be non-negative.");
    }
}

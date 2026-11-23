using Genelife.Values;

namespace Genelife.Family;

/// <summary>
/// Reproduction policy. Conception chance is a balance lever; the default is
/// set to replace population alongside immigration rather than cause a boom.
/// </summary>
public sealed class ReproductionOptions
{
    public int MinimumConceptionAge { get; set; } = 18;
    public int MaximumConceptionAge { get; set; } = 45;
    public int MinimumConceptionHealth { get; set; } = 50;
    public decimal MonthlyConceptionChance { get; set; } = 0.01m;
    public decimal MonthlyLossChance { get; set; } = 0.01m;
    public decimal LossCooldownMonths { get; set; } = 3m;
    public int PregnancyMonths { get; set; } = 9;
    public Money MonthlyPrenatalCareCost { get; set; } = Money.FromUnits(40m);
    public Money DeliveryCost { get; set; } = Money.Zero;
    public bool AllowSameSexCarryingPair { get; set; }
    public bool ChildBenefitEnabled { get; set; }
    public Money ChildBenefitMonthlyPerChild { get; set; } = Money.Zero;

    public static ReproductionOptions Default { get; } = new();

    public void Validate()
    {
        if (MinimumConceptionAge < 0)
            throw new ArgumentOutOfRangeException(nameof(MinimumConceptionAge), "MinimumConceptionAge must be non-negative.");
        if (MaximumConceptionAge < MinimumConceptionAge)
            throw new ArgumentOutOfRangeException(nameof(MaximumConceptionAge), "MaximumConceptionAge must be at least the minimum.");
        if (MinimumConceptionHealth < 0 || MinimumConceptionHealth > 100)
            throw new ArgumentOutOfRangeException(nameof(MinimumConceptionHealth), "MinimumConceptionHealth must be within 0..100.");
        if (MonthlyConceptionChance < 0m || MonthlyConceptionChance > 1m)
            throw new ArgumentOutOfRangeException(nameof(MonthlyConceptionChance), "MonthlyConceptionChance must be between 0 and 1.");
        if (MonthlyLossChance < 0m || MonthlyLossChance > 1m)
            throw new ArgumentOutOfRangeException(nameof(MonthlyLossChance), "MonthlyLossChance must be between 0 and 1.");
        if (LossCooldownMonths < 0m)
            throw new ArgumentOutOfRangeException(nameof(LossCooldownMonths), "LossCooldownMonths must be non-negative.");
        if (PregnancyMonths < 1)
            throw new ArgumentOutOfRangeException(nameof(PregnancyMonths), "PregnancyMonths must be positive.");
        if (MonthlyPrenatalCareCost.IsNegative || DeliveryCost.IsNegative ||
            ChildBenefitMonthlyPerChild.IsNegative)
            throw new ArgumentOutOfRangeException(nameof(MonthlyPrenatalCareCost), "Reproduction money options must be non-negative.");
    }
}

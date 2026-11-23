using Genelife.Values;

namespace Genelife.Family;

public sealed class InheritanceOptions
{
    public decimal PartnerShare { get; set; } = 0.50m;
    public decimal ChildSharePool { get; set; } = 0.50m;
    public bool SplitChildPoolEqually { get; set; } = true;
    public int ExtendedFamilyDepth { get; set; } = 2;
    public Money MinorTrustMinimum { get; set; } = Money.FromUnits(100m);

    public static InheritanceOptions Default { get; } = new();

    public void Validate()
    {
        if (PartnerShare < 0m || PartnerShare > 1m)
            throw new ArgumentOutOfRangeException(nameof(PartnerShare), "PartnerShare must be between 0 and 1.");
        if (ChildSharePool < 0m || ChildSharePool > 1m)
            throw new ArgumentOutOfRangeException(nameof(ChildSharePool), "ChildSharePool must be between 0 and 1.");
        if (ExtendedFamilyDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(ExtendedFamilyDepth), "ExtendedFamilyDepth must be non-negative.");
        if (MinorTrustMinimum.IsNegative)
            throw new ArgumentOutOfRangeException(nameof(MinorTrustMinimum), "MinorTrustMinimum must be non-negative.");
    }
}

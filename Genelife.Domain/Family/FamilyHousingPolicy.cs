namespace Genelife.Family;

public sealed class FamilyHousingPolicy
{
    public decimal AdultChildMoveOutShare { get; set; } = 0.50m;
    public int MinimumHomeCapacityForAdultChild { get; set; } = 1;

    public static FamilyHousingPolicy Default { get; } = new();

    public void Validate()
    {
        if (AdultChildMoveOutShare < 0m || AdultChildMoveOutShare > 1m)
            throw new ArgumentOutOfRangeException(nameof(AdultChildMoveOutShare), "AdultChildMoveOutShare must be between 0 and 1.");
        if (MinimumHomeCapacityForAdultChild < 0)
            throw new ArgumentOutOfRangeException(nameof(MinimumHomeCapacityForAdultChild), "MinimumHomeCapacityForAdultChild must be non-negative.");
    }
}

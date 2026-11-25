namespace Genelife.Enums;

/// <summary>
/// Position of a member inside a household. Only the head and adult members can
/// be charged for housing costs.
/// </summary>
public enum HouseholdRole
{
    Head,
    Partner,
    Child,
    Relative,
    Other
}

using Arch.Core;
using Genelife.Enums;

namespace Genelife.Components.Housing;

/// <summary>
/// Membership edge between a sim and a household. A sim may have at most one of
/// these components.
/// </summary>
public record struct HouseholdMember
{
    public Entity Household;
    public HouseholdRole Role;

    public HouseholdMember(Entity household, HouseholdRole role)
    {
        Household = household;
        Role = role;
    }
}

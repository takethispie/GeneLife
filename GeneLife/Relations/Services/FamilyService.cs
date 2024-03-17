using GeneLife.Relations.Components;

namespace GeneLife.Relations.Services;

public static class FamilyService
{
    public static bool AreRelated(FamilyMember first, FamilyMember second)
    {
        return first.FamilyId == second.FamilyId;
    }
}
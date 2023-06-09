using GeneLife.Oracle.Components;

namespace GeneLife.Oracle.Services;

public static class FamilyService
{
    public static bool AreRelated(FamilyMember first, FamilyMember second)
    {
        return first.FamilyId == second.FamilyId;
    }
}
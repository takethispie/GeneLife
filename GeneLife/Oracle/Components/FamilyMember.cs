using GeneLife.Oracle.Core;

namespace GeneLife.Oracle.Components;

/// <summary>
/// member of a local family
/// (not a whole family tree, usually a family in a house)
/// </summary>
public struct FamilyMember
{
    public int FamilyId;
    public int Generation;
    public FamilyMemberType MemberType;
}
using GeneLife.Relations;

namespace GeneLife.Relations.Components;

/// <summary>
/// member of a local family
/// (not a whole family tree, usually a family in a house)
/// </summary>
public struct FamilyMember
{
    public Guid FamilyId;
    public int Generation;
    public FamilyMemberType MemberType;
}
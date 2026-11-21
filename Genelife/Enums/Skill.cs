namespace Genelife.Enums;

/// <summary>
/// Skill tokens stored as a bitmask inside hot employment components. Keeping
/// skills as flags avoids <see cref="List{T}"/> allocations and reference
/// fields in components that are touched every hiring pass.
/// </summary>
[Flags]
public enum Skill : ushort
{
    None = 0,
    Programming = 1 << 0,
    Art = 1 << 1,
    Cooking = 1 << 2,
    Medical = 1 << 3,
    Engineering = 1 << 4,
    Sales = 1 << 5,
    Rare = 1 << 6,
}

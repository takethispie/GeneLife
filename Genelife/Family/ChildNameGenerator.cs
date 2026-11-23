namespace Genelife.Family;

/// <summary>
/// Deterministic child names keyed by the child's network id so births and
/// restores produce the same name without a shared RNG.
/// </summary>
public static class ChildNameGenerator
{
    private static readonly string[] GivenNames =
    [
        "Ada", "Ari", "Bea", "Bo", "Cleo", "Dara", "Eli", "Emi", "Faye", "Finn",
        "Gia", "Hugo", "Iris", "Jade", "Kai", "Lena", "Leo", "Maya", "Nico", "Nora",
        "Oren", "Pia", "Quinn", "Remy", "Rosa", "Sam", "Sena", "Theo", "Una", "Vale",
        "Wren", "Yara", "Zane", "Zoe", "Ash", "Luca", "Mila", "Noa", "Rin", "Sol"
    ];

    private static readonly string[] FamilyNames =
    [
        "Vale", "Rowan", "Hart", "Lark", "Moss", "Reed", "Stone", "Brook", "Frost", "Wells"
    ];

    public static string For(ulong childNetworkId)
    {
        var given = GivenNames[StableIndex(childNetworkId, GivenNames.Length, 17)];
        var family = FamilyNames[StableIndex(childNetworkId, FamilyNames.Length, 31)];
        return $"{given} {family}";
    }

    private static int StableIndex(ulong value, int length, int salt)
    {
        var mixed = value ^ ((ulong)salt << 32);
        mixed ^= mixed >> 33;
        mixed *= 0xff51afd7ed558ccdUL;
        mixed ^= mixed >> 33;
        return (int)(mixed % (ulong)length);
    }
}

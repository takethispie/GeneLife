using Genelife.Components.Gen;

namespace Genelife.Family;

/// <summary>
/// Validates the canonical trait component. Every defined enum combination is
/// legal in V4a; undefined numeric values are not. Missing traits on a living
/// sim are a migration/creation defect handled by callers and invariants.
/// </summary>
public static class TraitValidation
{
    public static IReadOnlyList<string> Validate(in Traits traits)
    {
        var errors = new List<string>(10);
        Check(errors, traits.EyeColor, nameof(Traits.EyeColor));
        Check(errors, traits.HairColor, nameof(Traits.HairColor));
        Check(errors, traits.Handedness, nameof(Traits.Handedness));
        Check(errors, traits.Morphotype, nameof(Traits.Morphotype));
        Check(errors, traits.Aptitude, nameof(Traits.Aptitude));
        Check(errors, traits.HeightPotential, nameof(Traits.HeightPotential));
        Check(errors, traits.Sex, nameof(Traits.Sex));
        Check(errors, traits.Temperament, nameof(Traits.Temperament));
        Check(errors, traits.SkinTone, nameof(Traits.SkinTone));
        Check(errors, traits.FaceShape, nameof(Traits.FaceShape));
        return errors;
    }

    public static bool IsValid(in Traits traits) => Validate(traits).Count == 0;

    public static void EnsureValid(in Traits traits, string context)
    {
        var errors = Validate(traits);
        if (errors.Count == 0)
            return;

        throw new InvalidOperationException(
            $"Invalid traits for {context}: {string.Join("; ", errors)}");
    }

    public static void EnsureValid(in Traits traits, ulong networkId) =>
        EnsureValid(traits, $"network id {networkId}");

    private static void Check<T>(List<string> errors, T value, string field)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
            errors.Add($"{field} has undefined value {Convert.ToInt32(value)}");
    }
}

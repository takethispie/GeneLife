using Genelife.Components.Gen;

namespace Genelife.Family;

/// <summary>
/// Boundary mapping between the canonical <see cref="Traits"/> component and the
/// offline <c>AttractivenessService</c> prototype. The simulation never takes a
/// Microsoft.ML dependency; only the calibration tool uses this mapping.
/// </summary>
public static class TraitMapping
{
    public const string EyeColorColumn = "EyeColor";
    public const string HairTypeColumn = "HairType";
    public const string HandednessColumn = "Handedness";
    public const string MorphotypeColumn = "Morphotype";
    public const string IntelligenceColumn = "Intelligence";
    public const string HeightPotentialColumn = "HeightPotential";
    public const string AgeColumn = "Age";
    public const string SexColumn = "Sex";
    public const string BehaviorPropensionColumn = "BehaviorPropension";

    public static readonly IReadOnlyList<string> PrototypeColumns =
    [
        EyeColorColumn,
        HairTypeColumn,
        HandednessColumn,
        MorphotypeColumn,
        IntelligenceColumn,
        HeightPotentialColumn,
        AgeColumn,
        SexColumn,
        BehaviorPropensionColumn
    ];

    /// <summary>
    /// Maps one side of a prototype row. Age must come from
    /// <see cref="Genelife.Components.Gen.Alive"/>; it is never part of
    /// <see cref="Traits"/>.
    /// </summary>
    public static IReadOnlyDictionary<string, object> ToPrototypeRow(in Traits traits, int age)
    {
        if (age < 0)
            throw new ArgumentOutOfRangeException(nameof(age), "Age must be non-negative.");

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [EyeColorColumn] = traits.EyeColor.ToString(),
            [HairTypeColumn] = traits.HairColor.ToString(),
            [HandednessColumn] = traits.Handedness.ToString(),
            [MorphotypeColumn] = traits.Morphotype.ToString(),
            [IntelligenceColumn] = traits.Aptitude.ToString(),
            [HeightPotentialColumn] = traits.HeightPotential.ToString(),
            [AgeColumn] = (float)age,
            [SexColumn] = traits.Sex.ToString(),
            [BehaviorPropensionColumn] = traits.Temperament.ToString()
        };
    }

    public static IEnumerable<KeyValuePair<string, object>> ToPrototypeRow(string prefix, Traits traits, int age)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Prefix is required.", nameof(prefix));

        foreach (var pair in ToPrototypeRow(traits, age))
            yield return new KeyValuePair<string, object>($"{prefix}_{pair.Key}", pair.Value);
    }
}

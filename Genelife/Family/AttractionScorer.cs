using Genelife.Components.Gen;

namespace Genelife.Family;

/// <summary>
/// Named components of a deterministic integer attraction score. The full
/// candidate-generation and pairing integration lives in feature 02; this
/// scorer only owns the explainable score arithmetic.
/// </summary>
public readonly record struct AttractionScore(
    int Total,
    int TraitCompatibility,
    int AgePreference,
    int Proximity,
    int Stability,
    int Health,
    int Availability);

public static class AttractionScorer
{
    public const int ScoreScale = 10_000;

    public static AttractionScore Score(
        in Traits first,
        int firstAge,
        in Traits second,
        int secondAge,
        int proximityBasisPoints,
        int stabilityBasisPoints,
        int healthBasisPoints,
        bool bothAvailable,
        AttractionCoefficients? coefficients = null)
    {
        coefficients ??= AttractionCoefficients.Default;
        coefficients.Validate();

        var compatibility = TraitCompatibility(first, second, coefficients);
        var age = AgePreference(firstAge, secondAge, coefficients.AgePreference);
        var proximity = Scale(proximityBasisPoints, coefficients.Proximity);
        var stability = Scale(stabilityBasisPoints, coefficients.Stability);
        var health = Scale(healthBasisPoints, coefficients.Health);
        var availability = bothAvailable ? coefficients.Availability : 0;

        var total = Math.Clamp(
            compatibility + age + proximity + stability + health + availability,
            0,
            ScoreScale);

        return new AttractionScore(total, compatibility, age, proximity, stability, health, availability);
    }

    public static int TraitCompatibility(
        in Traits first,
        in Traits second,
        AttractionCoefficients? coefficients = null)
    {
        coefficients ??= AttractionCoefficients.Default;

        var weight = 0;
        var matching = 0;

        void Add(int traitWeight, bool isMatch)
        {
            weight += traitWeight;
            if (isMatch)
                matching += traitWeight;
        }

        Add(coefficients.EyeColorWeight, first.EyeColor == second.EyeColor);
        Add(coefficients.HairColorWeight, first.HairColor == second.HairColor);
        Add(coefficients.HandednessWeight, first.Handedness == second.Handedness);
        Add(coefficients.MorphotypeWeight, first.Morphotype == second.Morphotype);
        Add(coefficients.AptitudeWeight, first.Aptitude == second.Aptitude);
        Add(coefficients.HeightPotentialWeight, first.HeightPotential == second.HeightPotential);
        Add(coefficients.TemperamentWeight, first.Temperament == second.Temperament);

        if (weight == 0)
            return coefficients.TraitCompatibility;

        return (int)Math.Round(coefficients.TraitCompatibility * (matching / (double)weight));
    }

    private static int AgePreference(int firstAge, int secondAge, int componentWeight)
    {
        var gap = Math.Abs(firstAge - secondAge);
        var similarity = Math.Max(0, 100 - gap * 3);
        return Scale(similarity * 100, componentWeight);
    }

    private static int Scale(int basisPoints, int componentWeight)
    {
        var clamped = Math.Clamp(basisPoints, 0, ScoreScale);
        return (int)(((long)clamped * componentWeight + ScoreScale / 2) / ScoreScale);
    }
}

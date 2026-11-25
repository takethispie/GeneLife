namespace Genelife.Family;

/// <summary>
/// Runtime attraction coefficients. They are generated offline by the
/// AttractivenessService calibration tool and checked in; the simulation never
/// loads an ML.NET model or takes a Microsoft.ML dependency.
/// </summary>
public sealed class AttractionCoefficients
{
    public const string CurrentVersion = "v4-attraction-2";

    public string Version { get; init; } = CurrentVersion;
    public int ScoreScale { get; init; } = 10_000;

    public int TraitCompatibility { get; init; } = 4_200;
    public int AgePreference { get; init; } = 1_800;
    public int Proximity { get; init; } = 1_200;
    public int Stability { get; init; } = 1_500;
    public int Health { get; init; } = 800;
    public int Availability { get; init; } = 500;

    public int EyeColorWeight { get; init; } = 100;
    public int HairColorWeight { get; init; } = 250;
    public int HandednessWeight { get; init; } = 100;
    public int MorphotypeWeight { get; init; } = 200;
    public int AptitudeWeight { get; init; } = 200;
    public int HeightPotentialWeight { get; init; } = 150;
    public int TemperamentWeight { get; init; } = 250;

    public static AttractionCoefficients Default { get; } = new();

    public void Validate()
    {
        if (ScoreScale != 10_000)
            throw new ArgumentOutOfRangeException(nameof(ScoreScale), "ScoreScale must be 10000 basis points.");

        if (Version != CurrentVersion)
            throw new InvalidOperationException(
                $"Attraction coefficient version '{Version}' does not match runtime '{CurrentVersion}'.");

        if (TraitCompatibility < 0 || AgePreference < 0 || Proximity < 0 ||
            Stability < 0 || Health < 0 || Availability < 0)
            throw new ArgumentOutOfRangeException(nameof(TraitCompatibility), "Component weights must be non-negative.");

        if (EyeColorWeight < 0 || HairColorWeight < 0 || HandednessWeight < 0 ||
            MorphotypeWeight < 0 || AptitudeWeight < 0 || HeightPotentialWeight < 0 ||
            TemperamentWeight < 0)
            throw new ArgumentOutOfRangeException(nameof(EyeColorWeight), "Trait weights must be non-negative.");
    }
}

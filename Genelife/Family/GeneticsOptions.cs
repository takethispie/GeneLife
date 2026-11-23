using Genelife.Components.Gen;

namespace Genelife.Family;

/// <summary>
/// Tunable genetics policy. Weights are non-negative, at least one positive per
/// trait, and normalized internally by the generator.
/// </summary>
public sealed class GeneticsOptions
{
    public decimal MutationRate { get; set; } = 0.04m;
    public decimal FemaleShare { get; set; } = 0.5m;

    public int[] AptitudeWeights { get; set; } = [1, 1, 1, 1, 1];
    public int[] TemperamentWeights { get; set; } = [50, 40, 10];
    public int[] HeightWeights { get; set; } = [25, 50, 25];
    public int[] EyeColorWeights { get; set; } = [40, 25, 20, 10, 5];
    public int[] HairColorWeights { get; set; } = [35, 30, 20, 10, 5];
    public int[] HandednessWeights { get; set; } = [88, 10, 2];
    public int[] MorphotypeWeights { get; set; } = [30, 45, 25];

    public static GeneticsOptions Default { get; } = new();

    public void Validate()
    {
        if (MutationRate < 0m || MutationRate > 1m)
            throw new ArgumentOutOfRangeException(
                nameof(MutationRate), "MutationRate must be between 0 and 1.");

        if (FemaleShare < 0m || FemaleShare > 1m)
            throw new ArgumentOutOfRangeException(
                nameof(FemaleShare), "FemaleShare must be between 0 and 1.");

        ValidateWeights(AptitudeWeights, nameof(AptitudeWeights), EnumValues<Aptitude>.Count);
        ValidateWeights(TemperamentWeights, nameof(TemperamentWeights), EnumValues<Temperament>.Count);
        ValidateWeights(HeightWeights, nameof(HeightWeights), EnumValues<HeightPotential>.Count);
        ValidateWeights(EyeColorWeights, nameof(EyeColorWeights), EnumValues<EyeColor>.Count);
        ValidateWeights(HairColorWeights, nameof(HairColorWeights), EnumValues<HairColor>.Count);
        ValidateWeights(HandednessWeights, nameof(HandednessWeights), EnumValues<Handedness>.Count);
        ValidateWeights(MorphotypeWeights, nameof(MorphotypeWeights), EnumValues<Morphotype>.Count);
    }

    public static void ValidateWeights(int[]? weights, string name, int expectedLength)
    {
        if (weights is null || weights.Length != expectedLength)
            throw new ArgumentException(
                $"{name} must contain {expectedLength} entries.", name);

        var total = 0L;
        foreach (var weight in weights)
        {
            if (weight < 0)
                throw new ArgumentOutOfRangeException(name, weight, $"{name} entries must be non-negative.");
            total += weight;
        }

        if (total <= 0)
            throw new ArgumentException($"{name} must have at least one positive weight.", name);
    }
}

internal static class EnumValues<T> where T : struct, Enum
{
    public static readonly T[] Items = Enum.GetValues<T>();
    public static int Count => Items.Length;
}

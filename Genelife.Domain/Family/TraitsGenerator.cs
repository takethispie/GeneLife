using Genelife.Components.Gen;

namespace Genelife.Family;

/// <summary>
/// Creation path for a trait set. The source only affects the deterministic
/// stream name; it never changes the trait schema.
/// </summary>
public enum TraitSource : byte
{
    Seed,
    Immigrant,
    Player,
    Admin,
    Migration,
    Inheritance
}

public static class TraitSourceStreams
{
    public static string For(TraitSource source) => source switch
    {
        TraitSource.Seed => "traits.seed",
        TraitSource.Immigrant => "traits.immigrant",
        TraitSource.Player => "traits.player",
        TraitSource.Admin => "traits.admin",
        TraitSource.Migration => "traits.migration",
        TraitSource.Inheritance => "traits.inheritance",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown trait source.")
    };
}

/// <summary>
/// Deterministic seed-time trait generation. The same world seed, network id,
/// source and options always produce the same traits, independent of creation
/// order.
/// </summary>
public static class TraitsGenerator
{
    public static Traits Create(
        int worldSeed,
        ulong networkId,
        TraitSource source,
        GeneticsOptions? options = null)
    {
        options ??= GeneticsOptions.Default;
        options.Validate();

        var random = new Random(DeriveSeed(worldSeed, TraitSourceStreams.For(source), networkId));

        return new Traits
        {
            EyeColor = Pick(random, options.EyeColorWeights, EnumValues<EyeColor>.Items),
            HairColor = Pick(random, options.HairColorWeights, EnumValues<HairColor>.Items),
            Handedness = Pick(random, options.HandednessWeights, EnumValues<Handedness>.Items),
            Morphotype = Pick(random, options.MorphotypeWeights, EnumValues<Morphotype>.Items),
            Aptitude = Pick(random, options.AptitudeWeights, EnumValues<Aptitude>.Items),
            HeightPotential = Pick(random, options.HeightWeights, EnumValues<HeightPotential>.Items),
            Sex = random.NextDouble() < (double)options.FemaleShare ? Sex.Female : Sex.Male,
            Temperament = Pick(random, options.TemperamentWeights, EnumValues<Temperament>.Items),
            SkinTone = Pick(random, options.SkinToneWeights, EnumValues<SkinTone>.Items),
            FaceShape = Pick(random, options.FaceShapeWeights, EnumValues<FaceShape>.Items)
        };
    }

    internal static int DeriveSeed(
        int worldSeed,
        string streamName,
        ulong networkId,
        string key = "")
    {
        return unchecked(worldSeed * 397 ^
                         SimulationRandom.StableHash($"{streamName}:{networkId}:{key}"));
    }

    private static T Pick<T>(Random random, int[] weights, T[] values)
    {
        long total = 0;
        foreach (var weight in weights)
            total += weight;

        var roll = random.NextInt64(total);
        for (var i = 0; i < weights.Length; i++)
        {
            roll -= weights[i];
            if (roll < 0)
                return values[i];
        }

        return values[^1];
    }
}

using Genelife.Components.Gen;

namespace Genelife.Appearance;

/// <summary>
/// Pure appearance derivation. The stable key is normally the sim network id;
/// the RNG is simulated with a stable hash so the result does not depend on
/// creation order, query order or Random.Shared.
/// </summary>
public static class AppearanceDerivation
{
    public static AppearanceDescriptor Derive(
        in Traits traits,
        int age,
        ulong stableKey,
        byte appearanceVersion = AppearanceDescriptor.CurrentVersion)
    {
        var band = AgingAppearancePolicy.BandFor(age);
        var hairStyle = DeriveHairStyle(traits, band, stableKey, appearanceVersion);
        var bodyFrame = DeriveBodyFrame(traits.Morphotype);
        var heightScale = DeriveHeightScale(traits.HeightPotential, band);
        var hairColor = DeriveHairColor(traits.HairColor, band, stableKey, appearanceVersion);
        var expression = DeriveExpression(traits.Temperament, stableKey, appearanceVersion);

        var descriptor = new AppearanceDescriptor(
            traits.SkinTone,
            traits.FaceShape,
            hairColor,
            hairStyle,
            traits.EyeColor,
            bodyFrame,
            heightScale,
            band,
            expression,
            appearanceVersion);

        AppearanceValidation.EnsureValid(descriptor, $"stable key {stableKey}");
        return descriptor;
    }

    /// <summary>
    /// Deterministic gene backfill for legacy sims. Existing traits are never
    /// read or changed; only the two new appearance genes are generated.
    /// </summary>
    public static (SkinTone SkinTone, FaceShape FaceShape) BackfillGenes(
        int worldSeed,
        ulong networkId,
        byte appearanceVersion = AppearanceDescriptor.CurrentVersion)
    {
        var skin = (SkinTone)(Draw(worldSeed, networkId, appearanceVersion, "skin") % 6);
        var face = (FaceShape)(Draw(worldSeed, networkId, appearanceVersion, "face") % 5);
        return (skin, face);
    }

    private static HairStyle DeriveHairStyle(
        in Traits traits,
        AgeBand band,
        ulong stableKey,
        byte version)
    {
        var draw = Draw(0, stableKey, version, "hair-style");
        if (band == AgeBand.Child)
            return (HairStyle)(draw % 3); // Short, Cropped, Wavy

        if (band == AgeBand.Senior && draw % 10 == 0)
            return HairStyle.Bald;

        // Older bands prefer shorter styles; the stable draw still picks within
        // the band so siblings are not identical.
        if (band is AgeBand.Middle or AgeBand.Senior)
        {
            var shortStyles = new[] { HairStyle.Short, HairStyle.Cropped, HairStyle.Straight, HairStyle.Braided };
            return shortStyles[draw % shortStyles.Length];
        }

        return (HairStyle)(draw % 6); // excludes Bald
    }

    private static BodyFrame DeriveBodyFrame(Morphotype morphotype) => morphotype switch
    {
        Morphotype.Ectomorph => BodyFrame.Slim,
        Morphotype.Endomorph => BodyFrame.Sturdy,
        _ => BodyFrame.Average
    };

    private static byte DeriveHeightScale(HeightPotential potential, AgeBand band)
    {
        var baseHeight = potential switch
        {
            HeightPotential.Short => 78,
            HeightPotential.Tall => 98,
            _ => 88
        };
        var scale = baseHeight + AgingAppearancePolicy.HeightOffset(band);
        return (byte)Math.Clamp(scale, 20, 100);
    }

    private static HairColor DeriveHairColor(
        HairColor inherited,
        AgeBand band,
        ulong stableKey,
        byte version)
    {
        if (band == AgeBand.Senior)
            return HairColor.Grey;
        if (band == AgeBand.Middle && Draw(0, stableKey, version, "grey") % 4 == 0)
            return HairColor.Grey;
        return inherited;
    }

    private static Expression DeriveExpression(
        Temperament temperament,
        ulong stableKey,
        byte version) => temperament switch
    {
        Temperament.Peaceful => Expression.Warm,
        Temperament.Violent => Expression.Alert,
        _ => Draw(0, stableKey, version, "expression") % 2 == 0
            ? Expression.Calm
            : Expression.Serious
    };

    private static uint Draw(int worldSeed, ulong stableKey, byte version, string field) =>
        (uint)SimulationRandom.StableHash(
            $"{worldSeed}:appearance.derive:{version}:{stableKey}:{field}");
}

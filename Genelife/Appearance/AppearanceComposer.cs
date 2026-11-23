namespace Genelife.Appearance;

/// <summary>
/// Pure descriptor → layer-list composition. The same descriptor and role
/// always produce the same layers, independent of renderer or platform.
/// </summary>
public static class AppearanceComposer
{
    public static AppearanceLayers Compose(
        in AppearanceDescriptor descriptor,
        bool isPlayer = false)
    {
        AppearanceValidation.EnsureValid(descriptor, "appearance composition");

        return new AppearanceLayers
        {
            BodyPart = BodyPart(descriptor.AgeBand, descriptor.BodyFrame),
            SkinPalette = $"skin-{Lower(descriptor.SkinTone)}",
            FacePart = $"face-{Lower(descriptor.FaceShape)}",
            EyePart = $"eyes-{Lower(descriptor.EyeColor)}",
            HairPart = descriptor.HairStyle == HairStyle.Bald
                ? "hair-bald"
                : $"hair-{Lower(descriptor.HairStyle)}-{Lower(descriptor.HairColor)}",
            OutfitPart = OutfitPart(descriptor.AgeBand, isPlayer),
            ExpressionPart = $"expression-{Lower(descriptor.Expression)}",
            HeightScale = descriptor.HeightScale / 100f,
            LayerOrderVersion = AppearanceLayerContract.CurrentLayerOrderVersion
        };
    }

    /// <summary>Labelled-token fallback used when a descriptor fails validation.</summary>
    public static AppearanceLayers Fallback() => new()
    {
        BodyPart = "body-fallback",
        SkinPalette = "skin-neutral",
        FacePart = "face-fallback",
        EyePart = "eyes-fallback",
        HairPart = "hair-fallback",
        OutfitPart = "outfit-fallback",
        ExpressionPart = "expression-fallback",
        HeightScale = 0.8f,
        LayerOrderVersion = AppearanceLayerContract.CurrentLayerOrderVersion
    };

    private static string BodyPart(AgeBand band, BodyFrame frame) =>
        $"body-{Lower(band)}-{Lower(frame)}";

    private static string OutfitPart(AgeBand band, bool isPlayer) => isPlayer
        ? "outfit-player"
        : band switch
        {
            AgeBand.Child => "outfit-child",
            AgeBand.Teen => "outfit-student",
            AgeBand.Adult => "outfit-worker",
            AgeBand.Middle => "outfit-worker",
            _ => "outfit-senior"
        };

    private static string Lower<T>(T value) where T : struct, Enum =>
        value.ToString().ToLowerInvariant();
}

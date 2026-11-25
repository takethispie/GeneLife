using Genelife.Components.Gen;

namespace Genelife.Appearance;

public sealed record AppearanceResemblance(
    bool SharesSkinTone,
    bool SharesFaceShape,
    bool SharesHairColor,
    bool SharesEyeColor,
    int SharedFields,
    decimal Score);

/// <summary>
/// Resemblance check over inherited genes, not rendered pixels. Derived fields
/// (hair style, age band, expression) are deliberately excluded.
/// </summary>
public static class FamilyResemblance
{
    public static AppearanceResemblance Compare(
        in AppearanceDescriptor first,
        in AppearanceDescriptor second)
    {
        var skin = first.SkinTone == second.SkinTone;
        var face = first.FaceShape == second.FaceShape;
        var hair = first.HairColor == second.HairColor;
        var eye = first.EyeColor == second.EyeColor;
        var shared = (skin ? 1 : 0) + (face ? 1 : 0) + (hair ? 1 : 0) + (eye ? 1 : 0);

        return new AppearanceResemblance(
            skin,
            face,
            hair,
            eye,
            shared,
            shared / 4m);
    }

    public static bool SharesInheritedFields(
        in AppearanceDescriptor first,
        in AppearanceDescriptor second) =>
        Compare(first, second).SharedFields >= 2;
}

using Genelife.Components.Gen;

namespace Genelife.Appearance;

/// <summary>
/// Compact, deterministic appearance projection. It is derived from traits and
/// age; only SkinTone and FaceShape are inherited genes. No strings, assets or
/// player-authored fields live here.
/// </summary>
public readonly record struct AppearanceDescriptor(
    SkinTone SkinTone,
    FaceShape FaceShape,
    HairColor HairColor,
    HairStyle HairStyle,
    EyeColor EyeColor,
    BodyFrame BodyFrame,
    byte HeightScale,
    AgeBand AgeBand,
    Expression Expression,
    byte AppearanceVersion)
{
    public const byte CurrentVersion = 1;

    /// <summary>Stable FNV-1a hash used for composition caches; never persisted.</summary>
    public uint StableHash()
    {
        unchecked
        {
            var hash = 2166136261u;
            Mix(ref hash, (byte)SkinTone);
            Mix(ref hash, (byte)FaceShape);
            Mix(ref hash, (byte)HairColor);
            Mix(ref hash, (byte)HairStyle);
            Mix(ref hash, (byte)EyeColor);
            Mix(ref hash, (byte)BodyFrame);
            Mix(ref hash, HeightScale);
            Mix(ref hash, (byte)AgeBand);
            Mix(ref hash, (byte)Expression);
            Mix(ref hash, AppearanceVersion);
            return hash;
        }
    }

    private static void Mix(ref uint hash, byte value)
    {
        hash ^= value;
        hash *= 16777619u;
    }
}

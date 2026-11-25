namespace Genelife.Appearance;

/// <summary>
/// Ordered atlas/palette contract shared by the browser, the admin SVG renderer
/// and tests. Part ids reference shared regions; palette ids reference shared
/// colour ramps. No id is a per-genos file.
/// </summary>
public sealed record AppearanceLayers
{
    public required string BodyPart { get; init; }
    public required string SkinPalette { get; init; }
    public required string FacePart { get; init; }
    public required string EyePart { get; init; }
    public required string HairPart { get; init; }
    public required string OutfitPart { get; init; }
    public required string ExpressionPart { get; init; }
    public float HeightScale { get; init; }
    public int LayerOrderVersion { get; init; }
}

public static class AppearanceLayerContract
{
    public const int CurrentLayerOrderVersion = 1;

    public static readonly string[] DrawOrder =
    [
        "body",
        "skin",
        "outfit",
        "face",
        "eyes",
        "hair",
        "expression"
    ];

    public static bool IsKnownPart(string? part) =>
        !string.IsNullOrWhiteSpace(part) &&
        part.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') &&
        part.Contains('-', StringComparison.Ordinal);
}

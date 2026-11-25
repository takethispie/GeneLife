namespace Genelife.Api.DTOs;

/// <summary>
/// Compact appearance block. Every field is numeric and fixed-size; no traits,
/// family data, strings or per-genos assets are included.
/// </summary>
public sealed record AppearanceDto
{
    public byte SkinTone { get; init; }
    public byte FaceShape { get; init; }
    public byte HairColor { get; init; }
    public byte HairStyle { get; init; }
    public byte EyeColor { get; init; }
    public byte BodyFrame { get; init; }
    public byte HeightScale { get; init; }
    public byte AgeBand { get; init; }
    public byte Expression { get; init; }
    public byte AppearanceVersion { get; init; }
}

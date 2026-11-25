namespace Genelife.Api.DTOs;

public sealed record AppearanceLayersDto(
    string BodyPart,
    string SkinPalette,
    string FacePart,
    string EyePart,
    string HairPart,
    string OutfitPart,
    string ExpressionPart,
    float HeightScale,
    int LayerOrderVersion);

public sealed record AppearanceGenosStatus(
    ulong NetworkId,
    string Name,
    int Age,
    bool IsPlayerControlled,
    string? PlayerId,
    string Stage,
    AppearanceDto Descriptor,
    AppearanceLayersDto Layers);

public sealed record AppearanceFamilyMemberStatus(
    string Relation,
    AppearanceGenosStatus Genos,
    bool SharesSkinTone,
    bool SharesFaceShape,
    bool SharesHairColor,
    bool SharesEyeColor,
    int SharedFields,
    decimal ResemblanceScore);

public sealed record AppearanceFamilyStatus(
    AppearanceGenosStatus Focal,
    IReadOnlyList<AppearanceFamilyMemberStatus> Members);

public sealed record AppearancePage(
    IReadOnlyList<AppearanceGenosStatus> Items,
    int Total);

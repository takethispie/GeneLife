namespace Genelife.Map;

public sealed record MapGenerationOptions
{
    public static readonly MapGenerationOptions Default = new();

    // Road pitch is derived from the largest footprint a lot must contain, so the
    // footprint sizing and the lot size move together (doc Part 1.3 / 5.1): a
    // footprint that fits no lot makes world generation throw. Lot interior is
    // (RoadSpacing - 1) tiles, so this leaves a 23x23 lot.
    public int RoadSpacing { get; init; } = BuildingFootprints.MaxDimension + 1;
    public int BlockPadding { get; init; } = 2;
    public float WaterFraction { get; init; } = 0.02f;
    public int MinLotTiles { get; init; } = 12;
    public int ResidentialShare { get; init; } = 50;
    public int CommercialShare { get; init; } = 25;
    public int EducationShare { get; init; } = 10;
    public int CivicShare { get; init; } = 15;
}

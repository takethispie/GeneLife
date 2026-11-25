namespace Genelife.Map;

public sealed record MapGenerationOptions
{
    public static readonly MapGenerationOptions Default = new();

    public int RoadSpacing { get; init; } = 12;
    public int BlockPadding { get; init; } = 2;
    public float WaterFraction { get; init; } = 0.02f;
    public int MinLotTiles { get; init; } = 12;
    public int ResidentialShare { get; init; } = 50;
    public int CommercialShare { get; init; } = 25;
    public int EducationShare { get; init; } = 10;
    public int CivicShare { get; init; } = 15;
}

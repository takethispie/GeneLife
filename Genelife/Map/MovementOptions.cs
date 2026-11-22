namespace Genelife.Map;

public sealed record MovementOptions
{
    public static readonly MovementOptions Default = new();
    public float PlayerSpeedTilesPerSecond { get; init; } = 4f;
    public int PathRequestBudgetPerTick { get; init; } = 32;
    public int MaxPathLength { get; init; } = 4096;
}

namespace Genelife.Map;

public sealed record MovementOptions
{
    public static readonly MovementOptions Default = new();
    public float PlayerSpeedTilesPerSecond { get; init; } = 4f;
    public int PathRequestBudgetPerTick { get; init; } = 32;
    /// <summary>
    /// Hard cap on pathfinder expansions per tick across all requests. A single
    /// pathological path can no longer consume the whole tick budget.
    /// </summary>
    public int MaxPathIterationsPerTick { get; init; } = 16_384;
    public int MaxPathLength { get; init; } = 4096;
}

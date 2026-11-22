namespace Genelife.Map;

public enum CommuteMode
{
    Abstract = 0,
    Animate = 1
}

public sealed record CommuteOptions
{
    public static readonly CommuteOptions Default = new();
    public CommuteMode Mode { get; init; } = CommuteMode.Abstract;
    public float SpeedTilesPerSecond { get; init; } = 3f;
}

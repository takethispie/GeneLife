using Genelife.Map;

namespace Genelife.Components.Map;

public record struct MovementState
{
    public TileCoord From;
    public TileCoord To;
    public float Progress;
    public float SpeedTilesPerSecond;
    public bool IsMoving;

    public MovementState(float speedTilesPerSecond)
    {
        From = TileCoord.Zero;
        To = TileCoord.Zero;
        Progress = 0f;
        SpeedTilesPerSecond = speedTilesPerSecond;
        IsMoving = false;
    }
}

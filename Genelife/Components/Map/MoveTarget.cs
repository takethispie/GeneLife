using Genelife.Map;

namespace Genelife.Components.Map;

public record struct MoveTarget
{
    public TileCoord Tile;
    public bool HasTarget;
    public long RequestedTick;
    public int PathVersion;

    public MoveTarget(TileCoord tile, long requestedTick = 0, int pathVersion = 0)
    {
        Tile = tile;
        HasTarget = true;
        RequestedTick = requestedTick;
        PathVersion = pathVersion;
    }
}

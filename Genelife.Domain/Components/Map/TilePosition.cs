namespace Genelife.Components.Map;

public record struct TilePosition
{
    public Genelife.Map.TileCoord Tile;

    public TilePosition(Genelife.Map.TileCoord tile)
    {
        Tile = tile;
    }
}

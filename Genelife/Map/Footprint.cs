namespace Genelife.Map;

/// <summary>
/// Width and height of a rectangular building footprint in tiles.
/// </summary>
public readonly record struct Footprint(int Width, int Height)
{
    public bool IsValid => Width > 0 && Height > 0;
    public int Area => Width * Height;

    public IEnumerable<TileCoord> Tiles(TileCoord anchor)
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
                yield return new TileCoord(anchor.X + x, anchor.Y + y);
        }
    }

    public override string ToString() => $"{Width}x{Height}";
}

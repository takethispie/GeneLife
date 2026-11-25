namespace Genelife.Map;

[Flags]
public enum TileFlags : byte
{
    None = 0,
    Land = 1,
    Road = 2,
    Occupied = 4,
    Reserved = 8,
    Water = 16
}

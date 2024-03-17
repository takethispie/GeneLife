
namespace GeneLife.Core.Components.Buildings;

public enum ShopType
{
    Grocery,
    Furniture
}

public readonly struct Shop(ShopType type)
{
    public ShopType Type { get; } = type;
}
using GeneLife.Core.Components.Buildings;

namespace GeneLife.Core.Items;

public struct Item(int id, ItemType itemType, string name, int price, ShopType shopType)
{
    public ItemType Type = itemType;
    public int Id = id;
    public string Name = name;
    public int Price = price;
    public ShopType ShopType = shopType;

    public readonly string Description() => $"Id: {Id} Type: {Type}";
}
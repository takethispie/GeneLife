using GeneLife.Core.Components.Buildings;

namespace GeneLife.Core.Items;

public struct Item
{
    public ItemType Type;
    public int Id;
    public string Name;
    public int Price;
    public ShopType ShopType;

    public Item(int id, ItemType itemType, string name, int price, ShopType shopType)
    {
        Id = id;
        Type = itemType;
        Name = name;
        Price = price;
        ShopType = shopType;
    }
}
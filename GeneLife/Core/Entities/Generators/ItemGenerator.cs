using GeneLife.Core.Components.Buildings;
using GeneLife.Core.Items;

namespace GeneLife.Core.Entities.Generators;

public static class ItemGenerator
{
    public static Item[] GetItemList() =>
        new[]
        {
            new Item(1, ItemType.Food, "Burger", 5, ShopType.Grocery),
            new Item(2, ItemType.Drink, "Water", 2, ShopType.Grocery)
        };
}
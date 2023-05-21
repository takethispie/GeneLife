using GeneLife.Core.Entities.Interfaces;
using GeneLife.Core.Items;

namespace GeneLife.Core.Entities.Generators;

public class BaseItemGenerator : IItemGenerator
{
    public Item[] GetItemList() =>
        new[]
        {
            new Item(1, ItemType.Food, "Burger"),
            new Item(2, ItemType.Drink, "Water")
        };
}
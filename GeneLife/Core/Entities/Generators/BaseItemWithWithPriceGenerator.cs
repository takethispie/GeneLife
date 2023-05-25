using GeneLife.Core.Entities.Interfaces;
using GeneLife.Core.Items;

namespace GeneLife.Core.Entities.Generators;

public class BaseItemWithWithPriceGenerator : IItemWithPriceGenerator
{
    public ItemWithPrice[] GetItemsWithPrice(Item[] items) =>
        new[]
        {
            new ItemWithPrice(items[0], 10),
            new ItemWithPrice(items[1], 5)
        };
}
using GeneLife.Core.Items;

namespace GeneLife.Core.Entities.Interfaces;

public interface IItemWithPriceGenerator
{
    public ItemWithPrice[] GetItemsWithPrice(Item[] items);
}
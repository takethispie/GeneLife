using GeneLife.Core.Items;

namespace GeneLife.Core.Components.Buildings;

public struct Shop
{
    public ItemWithPrice[] AvailableItems;

    public Shop()
    {
        AvailableItems = Array.Empty<ItemWithPrice>();
    }

    public Shop(ItemWithPrice[] availableItems)
    {
        AvailableItems = availableItems;
    }
}
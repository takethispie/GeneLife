using GeneLife.Core.Items;

namespace GeneLife.Common.Components;

public struct Inventory
{
    public Item[] items;

    public Inventory()
    {
        items = new Item[16];
    }
}
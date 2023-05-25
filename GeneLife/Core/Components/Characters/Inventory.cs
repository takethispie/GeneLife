using GeneLife.Core.Items;

namespace GeneLife.Core.Components.Characters;

public struct Inventory
{
    public Item[] items;

    public Inventory()
    {
        items = new Item[16];
    }
}
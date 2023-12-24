using GeneLife.Core.Items;

namespace GeneLife.Core.Components.Characters;

public struct Inventory
{
    private Item[] items;

    public Inventory()
    {
        items = new Item[16];
    }

    public bool Store(Item item)
    {
        var stored = false;
        items = items.Select(x => {
            if (x.Type == ItemType.None) {
                stored = true;
                return item;
            } 
            else return x;
        }).ToArray();
        return stored;
    }

    public readonly bool HasItemType(ItemType type) => items.Any(x => x.Type == type);

    public readonly bool HasItem(string name) => items.Any(item => item.Name.ToLower() == name.ToLower());

    public Item[] GetItems() => items;

    public Item? Take(ItemType type)
    {
        Item? item = null;
        items = items.Select(x =>
        {
            if(x.Type == type) {
                item = x;
                return new Item { Type = ItemType.None, Id = -1 };
            }
            return x;
        }).ToArray();
        return item;
    }

    public Item? Take(string name)
    {
        Item? item = null;
        items = items.Select(x =>
        {
            if (x.Name.ToLower() == name.ToLower())
            {
                item = x;
                return new Item { Type = ItemType.None, Id = -1 };
            }
            return x;
        }).ToArray();
        return item;
    }

    public readonly int Count => items.Count(x => x.Type != ItemType.None);
}
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
        var id = -1;
        Item? item = null;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i].Type == type)
            {
                item = items[i];
                id = i;
                break;
            }
        }
        if(id > -1) items[id] = new Item { Type = ItemType.None, Id = -1 };
        return item;
    }

    public Item? Take(string name)
    {
        var id = -1;
        Item? item = null;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i].Name.ToLower() == name.ToLower())
            {
                item = items[i];
                id = i;
                break;
            }
        }
        if (id > -1) items[id] = new Item { Type = ItemType.None, Id = -1 };
        return item;
    }

    public readonly int Count => items.Count(x => x.Type != ItemType.None);
}
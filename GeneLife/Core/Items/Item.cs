namespace GeneLife.Core.Items;

public struct Item
{
    public ItemType Type;
    public int Id;
    public string Name;

    public Item(int id, ItemType itemType, string name)
    {
        Id = id;
        Type = itemType;
        Name = name;
    }

    public Item(int id, ItemType type) : this()
    {
        Id = id;
        Type = type;
    }
}
using Genelife.Domain.Human;

namespace Genelife.Domain.Items;

public class Item
{
    public string Name { get; }
    public Guid Id { get; }
    public ItemCategory Category { get; }
    public decimal Value { get; private set; }
    public ItemQuality Quality { get; private set; }
    public bool IsStackable { get; }
    public int MaxStackSize { get; }
    public Dictionary<NeedType, float> UseEffects { get; }
    public int Durability { get; private set; }
    public int MaxDurability { get; }

    public Item(
        string name, 
        ItemCategory category, 
        decimal value, 
        ItemQuality quality = ItemQuality.Normal,
        bool isStackable = false,
        int maxStackSize = 1,
        int durability = 100)
    {
        Id = Guid.NewGuid();
        Name = name;
        Category = category;
        Value = value;
        Quality = quality;
        IsStackable = isStackable;
        MaxStackSize = maxStackSize;
        UseEffects = new Dictionary<NeedType, float>();
        Durability = durability;
        MaxDurability = durability;
    }

    public virtual bool Use()
    {
        if (Durability <= 0) return false;
        
        Durability--;
        if (Durability <= 0)
        {
            Value = 0;
            Quality = ItemQuality.Poor;
        }
        
        return true;
    }

    public void Repair()
    {
        Durability = MaxDurability;
        Quality = ItemQuality.Normal;
        Value = Value * 0.8m; // Repaired items are worth slightly less
    }
}

// Factory for creating common items
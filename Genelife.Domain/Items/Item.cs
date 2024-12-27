using Genelife.Domain.Human;

namespace Genelife.Domain.Items;

public class Item(
    string name,
    ItemCategory category,
    float value,
    ItemQuality quality = ItemQuality.Normal,
    bool isStackable = false,
    int maxStackSize = 1,
    int durability = 100) {
    public string Name { get; } = name;
    public Guid Id { get; } = Guid.NewGuid();
    public ItemCategory Category { get; } = category;
    public float Value { get; private set; } = value;
    public ItemQuality Quality { get; private set; } = quality;
    public bool IsStackable { get; } = isStackable;
    public int MaxStackSize { get; } = maxStackSize;
    public Dictionary<NeedType, float> UseEffects { get; } = new();
    public int Durability { get; private set; } = durability;
    public int MaxDurability { get; } = durability;

    public virtual bool Use()
    {
        if (Durability <= 0) return false;
        Durability--;
        if (Durability > 0) return true;
        Value = 0;
        Quality = ItemQuality.Poor;
        return true;
    }

    public void Repair()
    {
        Durability = MaxDurability;
        Quality = ItemQuality.Normal;
        Value = Value * 0.8f; // Repaired items are worth slightly less
    }
}

// Factory for creating common items
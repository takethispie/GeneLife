namespace Genelife.Domain.Items;

public class Inventory
{
    private readonly List<InventorySlot> slots;
    private readonly int maxSlots;

    public IReadOnlyList<InventorySlot> Slots => slots.AsReadOnly();
    public int UsedSlots => slots.Count;
    public int RemainingSlots => maxSlots - UsedSlots;

    public Inventory(int maxSlots = 20)
    {
        this.maxSlots = maxSlots;
        slots = new List<InventorySlot>();
    }

    public bool AddItem(Item item, int quantity = 1)
    {
        // Try to add to existing stack first
        if (item.IsStackable)
        {
            var existingSlot = slots.FirstOrDefault(s => s.Item.Name == item.Name && s.CanAddMore);
            if (existingSlot != null)
            {
                int added = existingSlot.AddItems(quantity);
                quantity -= added;
                if (quantity == 0) return true;
            }
        }

        // Create new slots for remaining items
        while (quantity > 0 && RemainingSlots > 0)
        {
            int stackSize = Math.Min(quantity, item.MaxStackSize);
            slots.Add(new InventorySlot(item, stackSize));
            quantity -= stackSize;
        }

        return quantity == 0;
    }

    public bool RemoveItem(string itemName, int quantity = 1)
    {
        var relevantSlots = slots.Where(s => s.Item.Name == itemName).ToList();
        int availableQuantity = relevantSlots.Sum(s => s.Quantity);

        if (availableQuantity < quantity) return false;

        foreach (var slot in relevantSlots)
        {
            int removed = slot.RemoveItems(quantity);
            quantity -= removed;
            if (slot.Quantity == 0)
            {
                slots.Remove(slot);
            }
            if (quantity == 0) break;
        }

        return true;
    }

    public Item FindItem(string itemName)
    {
        return slots.FirstOrDefault(s => s.Item.Name == itemName)?.Item;
    }

    public int GetItemCount(string itemName)
    {
        return slots.Where(s => s.Item.Name == itemName).Sum(s => s.Quantity);
    }

    public decimal GetTotalValue()
    {
        return slots.Sum(s => s.Item.Value * s.Quantity);
    }
}
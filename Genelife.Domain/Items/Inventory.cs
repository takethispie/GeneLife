namespace Genelife.Domain.Items;

public class Inventory(int maxSlots = 20) {
    private readonly List<InventorySlot> slots = new();

    public IReadOnlyList<InventorySlot> Slots => slots.AsReadOnly();
    public int UsedSlots => slots.Count;
    public int RemainingSlots => maxSlots - UsedSlots;

    public bool AddItem(Item item, int quantity = 1)
    {
        // Try to add to existing stack first
        if (item.IsStackable) {
            var existingSlot = slots.FirstOrDefault(s => s.Item.Name == item.Name && s.CanAddMore);
            if (existingSlot != null) {
                var added = existingSlot.AddItems(quantity);
                quantity -= added;
                if (quantity == 0) return true;
            }
        }

        // Create new slots for remaining items
        while (quantity > 0 && RemainingSlots > 0) {
            var stackSize = Math.Min(quantity, item.MaxStackSize);
            slots.Add(new(item, stackSize));
            quantity -= stackSize;
        }

        return quantity == 0;
    }
    

    public bool RemoveItem(string itemName, int quantity = 1)
    {
        var relevantSlots = slots.Where(s => s.Item.Name == itemName).ToList();
        var availableQuantity = relevantSlots.Sum(s => s.Quantity);

        if (availableQuantity < quantity) return false;

        foreach (var slot in relevantSlots)
        {
            var removed = slot.RemoveItems(quantity);
            quantity -= removed;
            if (slot.Quantity == 0) slots.Remove(slot);
            if (quantity == 0) break;
        }
        return true;
    }

    public Item? FindItem(string itemName) => slots.FirstOrDefault(s => s.Item.Name == itemName)?.Item;

    public int GetItemCount(string itemName) => 
        slots.Where(s => s.Item.Name == itemName).Sum(s => s.Quantity);

    public float GetTotalValue() => slots.Sum(s => s.Item.Value * s.Quantity);
}
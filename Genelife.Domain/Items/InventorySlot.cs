namespace Genelife.Domain.Items;

public class InventorySlot(Item item, int quantity = 1) {
    public Item Item { get; } = item;
    public int Quantity { get; private set; } = Math.Min(quantity, item.MaxStackSize);
    public bool CanAddMore => Item.IsStackable && Quantity < Item.MaxStackSize;

    public int AddItems(int amount)
    {
        if (!Item.IsStackable) return 0;
        var spaceAvailable = Item.MaxStackSize - Quantity;
        var itemsToAdd = Math.Min(amount, spaceAvailable);
        Quantity += itemsToAdd;
        return itemsToAdd;
    }
    

    public int RemoveItems(int amount)
    {
        var itemsToRemove = Math.Min(amount, Quantity);
        Quantity -= itemsToRemove;
        return itemsToRemove;
    }
}
namespace Genelife.Domain.Items;

public class InventorySlot
{
    public Item Item { get; }
    public int Quantity { get; private set; }

    public InventorySlot(Item item, int quantity = 1)
    {
        Item = item;
        Quantity = Math.Min(quantity, item.MaxStackSize);
    }

    public bool CanAddMore => Item.IsStackable && Quantity < Item.MaxStackSize;

    public int AddItems(int amount)
    {
        if (!Item.IsStackable) return 0;
        
        int spaceAvailable = Item.MaxStackSize - Quantity;
        int itemsToAdd = Math.Min(amount, spaceAvailable);
        Quantity += itemsToAdd;
        return itemsToAdd;
    }

    public int RemoveItems(int amount)
    {
        int itemsToRemove = Math.Min(amount, Quantity);
        Quantity -= itemsToRemove;
        return itemsToRemove;
    }
}
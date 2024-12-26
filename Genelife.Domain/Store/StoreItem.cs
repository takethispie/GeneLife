using Genelife.Domain.Items;

namespace Genelife.Domain.Store;

public class StoreItem
{
    public Item Item { get; }
    public int Quantity { get; set; }

    public StoreItem(Item item, int quantity)
    {
        Item = item;
        Quantity = quantity;
    }
}
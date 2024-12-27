using Genelife.Domain.Items;

namespace Genelife.Domain.Shop;

public class StoreItem(Item item, int quantity) {
    public Item Item { get; } = item;
    public int Quantity { get; set; } = quantity;
}
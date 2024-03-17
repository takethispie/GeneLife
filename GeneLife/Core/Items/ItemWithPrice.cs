namespace GeneLife.Core.Items;

public struct ItemWithPrice
{
    public Item Item;
    public int Price;

    public ItemWithPrice(Item item, int price)
    {
        Item = item;
        Price = price;
    }
}
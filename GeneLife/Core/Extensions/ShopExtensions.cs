using GeneLife.Core.Components.Buildings;
using GeneLife.Core.Items;

namespace GeneLife.Core.Extensions;

public static class ShopExtensions
{
    public static bool HasItemType(this Shop shop, ItemType type) => shop.AvailableItems.Any(x => x.Item.Type == type);

    public static ItemWithPrice[] ItemsPricedEqualOrUnder(this Shop shop, int price) 
        => shop.AvailableItems.Where(x => x.Price < price).ToArray();
}
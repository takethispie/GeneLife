using System.Numerics;
using Arch.Core;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Buildings;

namespace GeneLife.Demeter.Services;

public static class ShopSearchService
{
    public static (Position position, Adress adress, bool valid) NearestShopWithItem(World world, int itemid, Position npcPosition)
    {
        var closestDistance = 0f;
        (Position Position, Adress adress, bool valid) currentClosest = (new Position(), new Adress(), false); 
        var shopQuery = new QueryDescription().WithAll<Shop, Adress, Position>();
        world.ParallelQuery(in shopQuery, (ref Shop shop, ref Adress adress, ref Position position) =>
        {
            var distance = Vector3.Distance(position.Coordinates, npcPosition.Coordinates);
            if (!IsCurrentClosestShop(itemid, shop, distance, closestDistance)) return;
            closestDistance = distance;
            currentClosest = (position, adress, true);
        });
        return currentClosest.valid ? currentClosest : (new Position(), new Adress(), false);
    }

    private static bool IsCurrentClosestShop(int itemId, Shop shop, float distance, float closestDistance)
    {
        var hasItemNeeded = shop.AvailableItems.Any(itemWithPrice => itemWithPrice.Item.Id == itemId);
        return distance < closestDistance && hasItemNeeded;
    }
}
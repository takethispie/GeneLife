using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Buildings;

namespace GeneLife.Demeter.Services;

public static class ShopSearchService
{
    public static Entity? NearestShopWithItem(World world,
        IEnumerable<Entity> shops, int itemId, Position npcPosition)
    {
        var shopArray = shops.ToArray();
        if(!shopArray.Any()) return null;
        float closestDistance = -1;
        Entity? currentClosest = null; 
        foreach (var entity in shopArray)
        {
            var position = entity.Get<Position>();
            var shopComponent = entity.Get<Shop>();
            var distance = Vector3.Distance(position.Coordinates, npcPosition.Coordinates);
            if (!IsCurrentClosestShop(itemId, shopComponent, distance, closestDistance)) continue;
            closestDistance = distance;
            currentClosest = entity;
        }

        return currentClosest;
    }

    private static bool IsCurrentClosestShop(int itemId, Shop shop, float distance, float closestDistance)
    {
        var hasItemNeeded = shop.AvailableItems.Any(itemWithPrice => itemWithPrice.Item.Id == itemId);
        return distance < closestDistance && hasItemNeeded || hasItemNeeded && closestDistance < 0;
    }
}
using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Buildings;

namespace GeneLife.Services
{
    internal static class ShopSearchService
    {
        public static Entity? NearestShop(IEnumerable<Entity> shops, Position npcPosition)
        {
            var shopArray = shops.ToArray();
            if (shopArray.Length == 0) return null;
            float closestDistance = -1;
            Entity? currentClosest = null;
            foreach (var entity in shopArray)
            {
                var position = entity.Get<Position>();
                var shopComponent = entity.Get<Shop>();
                var distance = Vector3.Distance(position.Coordinates, npcPosition.Coordinates);
                if (distance < closestDistance) continue;
                closestDistance = distance;
                currentClosest = entity;
            }
            return currentClosest;
        }
    }
}
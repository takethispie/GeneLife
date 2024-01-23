using GeneLife.Core.Items;

namespace GeneLife.Core.Components.Buildings
{
    public enum ShopType
    {
        Grocery,
        Furniture
    }

    public struct Shop
    {
        public ShopType Type { get; }

        public Shop(ShopType type)
        {
            Type = type;
        }

    }
}
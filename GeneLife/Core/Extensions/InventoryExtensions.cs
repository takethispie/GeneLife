using GeneLife.Core.Components.Characters;
using GeneLife.Core.Items;

namespace GeneLife.Core.Extensions;

public static class InventoryExtensions
{
    public static int ItemsCount(this Inventory inventory) =>
        inventory.items.Count(x => x.Type != ItemType.None);
}
using Arch.Core;
using Genelife.Values;

namespace Genelife.Components.Consumption;

/// <summary>
/// One inventory row per store and catalog item. The owning store is linked by
/// <see cref="StoreStockLink"/>; entity storage avoids fixed-size component
/// arrays and keeps restock/spoilage per item.
/// </summary>
public record struct StoreInventory
{
    public int ItemIndex;
    public int Stock;
    public Money CurrentPrice;
    public long LastRestockTick;
}

public record struct StoreStockLink
{
    public Entity Store;
}

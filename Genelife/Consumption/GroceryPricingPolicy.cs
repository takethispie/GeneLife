using Genelife.Components.Consumption;
using Genelife.Values;

namespace Genelife.Consumption;

public sealed class GroceryOptions
{
    public int StockPerItem { get; set; } = 20;
    public int RestockThreshold { get; set; } = 8;
    public int RestockAmount { get; set; } = 12;
    public int SpoilageGraceTicks { get; set; } = 0;
    public decimal MinimumPriceMultiplier { get; set; } = 0.80m;
    public decimal MaximumPriceMultiplier { get; set; } = 1.40m;

    public static GroceryOptions Default { get; } = new();
}

public static class GroceryPricingPolicy
{
    public static Money Price(
        GroceryCatalogItem item,
        int stock,
        int maxStock,
        int monthOfYear,
        GroceryOptions? options = null)
    {
        options ??= GroceryOptions.Default;
        var stockRatio = maxStock <= 0 ? 0m : Math.Clamp(stock / (decimal)maxStock, 0m, 1m);
        var stockPressure = 1m + (1m - stockRatio) * 0.30m;
        var season = 0.95m + (monthOfYear % 12) * 0.01m; // 0.95..1.06
        var multiplier = Math.Clamp(
            stockPressure * season,
            options.MinimumPriceMultiplier,
            options.MaximumPriceMultiplier);
        return (item.BasePrice * multiplier).Max(Money.FromCents(1));
    }
}

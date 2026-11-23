using Genelife.Values;

namespace Genelife.Components.Consumption;

public enum GroceryCategory : byte { Staple, Produce, Protein, Prepared, Household }

public sealed record GroceryCatalogItem(
    string Id,
    GroceryCategory Category,
    int Nutrition,
    int ShelfLifeTicks,
    Money BasePrice,
    int UnitsPerPurchase,
    int PantryUnits);

public static class GroceryCatalog
{
    public const int Version = 1;

    public static readonly IReadOnlyList<GroceryCatalogItem> Items =
    [
        new("bread", GroceryCategory.Staple, 2, 300, Money.FromUnits(6m), 2, 2),
        new("rice", GroceryCategory.Staple, 3, 900, Money.FromUnits(4m), 1, 3),
        new("vegetables", GroceryCategory.Produce, 3, 240, Money.FromUnits(5m), 2, 2),
        new("fruit", GroceryCategory.Produce, 2, 300, Money.FromUnits(4m), 2, 2),
        new("protein", GroceryCategory.Protein, 4, 240, Money.FromUnits(9m), 2, 2),
        new("prepared-meal", GroceryCategory.Prepared, 2, 120, Money.FromUnits(8m), 1, 1),
        new("soap", GroceryCategory.Household, 0, 1200, Money.FromUnits(7m), 1, 0)
    ];

    public static bool TryGet(string? id, out GroceryCatalogItem item)
    {
        item = Items.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, id?.Trim(), StringComparison.OrdinalIgnoreCase))!;
        return item is not null;
    }

    public static int IndexOf(string id)
    {
        for (var index = 0; index < Items.Count; index++)
            if (string.Equals(Items[index].Id, id, StringComparison.OrdinalIgnoreCase))
                return index;
        return -1;
    }
}

using Genelife.Values;

namespace Genelife.Player;

public readonly record struct PlayerCatalogItem(
    string Id,
    string Name,
    Money Price,
    int FoodUnits,
    int HouseholdGoods,
    int Hunger,
    int Hygiene,
    int MaxQuantity);

/// <summary>
/// The small V5a shop catalog used by the player purchase/consume loop before
/// feature 03 replaces it with the full grocery catalog and store stock.
/// </summary>
public static class PlayerCatalog
{
    public const string FoodId = "food";
    public const string HouseholdGoodsId = "household-goods";

    public static readonly PlayerCatalogItem Food = new(
        FoodId,
        "Groceries",
        Money.FromUnits(25m),
        FoodUnits: 1,
        HouseholdGoods: 0,
        Hunger: 45,
        Hygiene: 0,
        MaxQuantity: 20);

    public static readonly PlayerCatalogItem HouseholdGoods = new(
        HouseholdGoodsId,
        "Household goods",
        Money.FromUnits(40m),
        FoodUnits: 0,
        HouseholdGoods: 1,
        Hunger: 0,
        Hygiene: 30,
        MaxQuantity: 20);

    public static bool TryGet(string? id, out PlayerCatalogItem item)
    {
        switch (id?.Trim().ToLowerInvariant())
        {
            case FoodId:
            case "groceries":
                item = Food;
                return true;
            case HouseholdGoodsId:
            case "goods":
                item = HouseholdGoods;
                return true;
            default:
                item = default;
                return false;
        }
    }
}

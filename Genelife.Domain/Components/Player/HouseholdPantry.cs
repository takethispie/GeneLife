namespace Genelife.Components.Player;

/// <summary>
/// Household-owned supplies. Purchases increment the pantry through the normal
/// wallet/ledger path; consumption decrements it and never moves money.
/// </summary>
public record struct HouseholdPantry
{
    public int FoodUnits;
    public int HouseholdGoods;
    public long LastPurchaseTick;
}

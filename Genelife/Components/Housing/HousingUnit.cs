using Arch.Core;
using Genelife.Enums;
using Genelife.Values;

namespace Genelife.Components.Housing;

/// <summary>
/// One tenancy-sized residential unit. <see cref="IsVacant"/> is maintained by
/// the housing registry; <see cref="OccupantCount"/> is a cached member count so
/// overcrowding checks are O(1).
/// </summary>
public record struct HousingUnit
{
    public Entity Property;
    public string UnitNumber;
    public HousingTier Tier;
    public int Capacity;
    public Money BaseRent;
    public float Condition;
    public bool IsVacant;
    public Entity Owner;
    public Money PurchasePrice;
    public int OccupantCount;
    public bool IsCondemned;

    public HousingUnit(
        Entity property,
        string unitNumber,
        HousingTier tier,
        int capacity,
        Money baseRent,
        float condition = 0.8f,
        bool isVacant = true,
        Entity owner = default,
        Money purchasePrice = default)
    {
        Property = property;
        UnitNumber = unitNumber;
        Tier = tier;
        Capacity = capacity;
        BaseRent = baseRent;
        Condition = Math.Clamp(condition, 0.1f, 1f);
        IsVacant = isVacant;
        Owner = owner == default ? Entity.Null : owner;
        PurchasePrice = purchasePrice;
        OccupantCount = 0;
    }

    public bool IsOvercrowded => OccupantCount > Capacity;
}

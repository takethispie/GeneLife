using Genelife.Values;

namespace Genelife.Components.Gen;

/// <summary>
/// Monthly consumption record. Purchase flags gate free need restoration in
/// ActionSystem: an NPC still performs Eating, but the action only restores
/// hunger if the household bought food this month.
/// </summary>
public record struct ConsumptionState
{
    public Money LastCycleSpent;
    public Money LifetimeSpent;
    public Money UnmetDemand;
    public long LastSpendTick;
    public bool FoodPurchased;
    public bool HygienePurchased;
    public bool LeisurePurchased;
    public bool HealthPurchased;
    public bool DurablePurchased;

    public void StartMonth()
    {
        FoodPurchased = false;
        HygienePurchased = false;
        LeisurePurchased = false;
        HealthPurchased = false;
        DurablePurchased = false;
    }
}

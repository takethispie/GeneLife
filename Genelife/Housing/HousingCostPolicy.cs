using Genelife.Components.Housing;
using Genelife.Enums;
using Genelife.Values;

namespace Genelife.Housing;

/// <summary>
/// Formulas for the recurring housing costs: utilities, upkeep, deposits,
/// moving fees and late fees. Keeping them in one place makes the read model and
/// the systems agree on every number.
/// </summary>
public static class HousingCostPolicy
{
    public static Money UtilityBill(
        HousingTier tier,
        int occupants,
        int capacity,
        int monthOfYear,
        float condition)
    {
        var baseUtility = HousingTierPolicy.BaseUtility(tier);
        var occupancyFactor = capacity <= 0
            ? 1m
            : Math.Clamp(occupants / (decimal)capacity, 0.5m, 1.5m);
        var seasonFactor = 0.9m + (monthOfYear % 6) * 0.04m;
        var luxurySurcharge = tier == HousingTier.Luxury ? Money.FromUnits(25m) : Money.Zero;
        var conditionFactor = Math.Clamp(0.85m + (decimal)condition * 0.15m, 0.85m, 1m);

        return (baseUtility.Multiply(occupancyFactor * seasonFactor * conditionFactor) + luxurySurcharge)
            .Max(Money.Zero);
    }

    public static Money MonthlyUpkeep(Property property, int unitCount)
    {
        var perUnit = Money.FromUnits(12m) * unitCount;
        var conditionPenalty = Money.FromUnits(property.MonthlyUpkeep.Units * (decimal)(1f - property.Condition));
        return (property.MonthlyUpkeep + perUnit + conditionPenalty).Max(Money.Zero);
    }

    public static Money LateFee(Money rent) => rent.Multiply(0.05m);

    public static Money Deposit(Money monthlyRent) => monthlyRent.Max(Money.Zero);

    public static Money MovingFee => Money.FromUnits(50m);

    public static Money ShelterFee(HousingTier tier) =>
        tier == HousingTier.Shelter ? HousingTierPolicy.BaseRent(HousingTier.Shelter) : Money.Zero;

    public static float ConditionDecayPerMonth => 0.012f;

    public static float RepairPerMonth => 0.03f;

    public static float ConditionFloor => 0.1f;

    public static float UninhabitableThreshold => 0.2f;

    /// <summary>
    /// Cost of moving a property from 0.0 to 1.0 condition, scaled by unit count.
    /// An admin repair purchases a fraction of that span.
    /// </summary>
    public static Money FullRepairCost(int unitCount) =>
        Money.FromUnits(250m * Math.Max(1, unitCount));

    public static float RepairGain(Money amount, int unitCount)
    {
        var full = FullRepairCost(unitCount);
        if (!full.IsPositive || !amount.IsPositive)
            return 0f;
        var fraction = Math.Clamp(amount.Units / full.Units, 0m, 1m);
        return (float)fraction;
    }
}

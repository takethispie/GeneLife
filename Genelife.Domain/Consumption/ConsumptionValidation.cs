using Genelife.Components.Consumption;

namespace Genelife.Consumption;

public static class ConsumptionValidation
{
    public static IReadOnlyList<string> Validate(in UtilityNetwork network)
    {
        var errors = new List<string>(6);
        Check(errors, network.Service, nameof(network.Service));
        Check(errors, network.State, nameof(network.State));
        if (network.Production < 0) errors.Add("Production must be non-negative.");
        if (network.Capacity < 0) errors.Add("Capacity must be non-negative.");
        if (network.Load < 0) errors.Add("Load must be non-negative.");
        if (network.Reserve < 0) errors.Add("Reserve must be non-negative.");
        return errors;
    }

    public static IReadOnlyList<string> Validate(in HouseholdMeter meter)
    {
        var errors = new List<string>(8);
        Check(errors, meter.WaterLevel, nameof(meter.WaterLevel));
        Check(errors, meter.ElectricityLevel, nameof(meter.ElectricityLevel));
        if (meter.WaterUsage < 0) errors.Add("WaterUsage must be non-negative.");
        if (meter.ElectricityUsage < 0) errors.Add("ElectricityUsage must be non-negative.");
        if (meter.Arrears.IsNegative)
            errors.Add("Arrears must be non-negative.");
        if (meter.ConsecutivePoorMonths < 0)
            errors.Add("ConsecutivePoorMonths must be non-negative.");
        return errors;
    }

    public static IReadOnlyList<string> Validate(in StoreInventory inventory)
    {
        var errors = new List<string>(3);
        if (inventory.ItemIndex < 0 || inventory.ItemIndex >= GroceryCatalog.Items.Count)
            errors.Add("ItemIndex is outside the catalog.");
        if (inventory.Stock < 0) errors.Add("Stock must be non-negative.");
        if (!inventory.CurrentPrice.IsPositive) errors.Add("CurrentPrice must be positive.");
        return errors;
    }

    public static IReadOnlyList<string> Validate(in NutritionState nutrition)
    {
        var errors = new List<string>(2);
        if (nutrition.Balance is < -100 or > 100)
            errors.Add("Nutrition balance must be in -100..100.");
        if (nutrition.ConsecutivePoorDays < 0)
            errors.Add("ConsecutivePoorDays must be non-negative.");
        return errors;
    }

    private static void Check<T>(List<string> errors, T value, string field)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
            errors.Add($"{field} has undefined value {Convert.ToInt32(value)}");
    }
}

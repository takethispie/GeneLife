using Genelife.Economy;
using Genelife.Values;

namespace Genelife.Housing;

/// <summary>
/// Yearly housing snapshot. Captured by the housing metrics system and exposed
/// to the admin health checks.
/// </summary>
public sealed record HousingSnapshot(
    long Tick,
    int Year,
    int Properties,
    int Units,
    int OccupiedUnits,
    int VacantUnits,
    int ShelterBeds,
    int ShelteredHouseholds,
    int HomelessHouseholds,
    int OvercrowdedUnits,
    int UnitsBelowCondition,
    decimal VacancyRate,
    decimal RentIndex,
    Money RentRoll,
    Money RentCollected,
    Money RentArrears,
    Money UtilityArrears,
    Money UpkeepPaid,
    int EvictionsThisYear,
    int ConstructionUnits,
    decimal MedianRentBurden,
    decimal ShelterOccupancy);

public static class HousingHealth
{
    public static List<HousingHealthCheck> Evaluate(HousingSnapshot? current)
    {
        var checks = new List<HousingHealthCheck>();
        if (current is null)
        {
            checks.Add(new HousingHealthCheck("snapshot", HealthLevel.Warn, "No housing snapshot yet."));
            return checks;
        }

        var households = current.OccupiedUnits + current.HomelessHouseholds;
        var homelessRate = households > 0 ? current.HomelessHouseholds / (decimal)households : 0m;
        var arrearsShare = current.RentRoll.IsPositive
            ? current.RentArrears.Cents / (decimal)current.RentRoll.Cents
            : 0m;
        var overcrowdedRate = current.Units > 0 ? current.OvercrowdedUnits / (decimal)current.Units : 0m;
        var conditionRate = current.Units > 0 ? current.UnitsBelowCondition / (decimal)current.Units : 0m;

        checks.Add(Band("vacancy-rate", current.VacancyRate, 0.02m, 0.10m, 0.01m));
        checks.Add(High("homeless-households", homelessRate, 0.03m, 0.07m));
        checks.Add(High("shelter-occupancy", current.ShelterOccupancy, 0.90m, 1.00m));
        checks.Add(High("rent-burden-median", current.MedianRentBurden, 0.40m, 0.55m));
        checks.Add(High("rent-arrears", arrearsShare, 0.10m, 0.25m));
        checks.Add(High("overcrowded-units", overcrowdedRate, 0.08m, 0.15m));
        checks.Add(High("units-below-condition", conditionRate, 0.05m, 0.12m));

        return checks;
    }

    private static HousingHealthCheck Band(
        string name,
        decimal value,
        decimal warnLow,
        decimal warnHigh,
        decimal failLow)
    {
        if (value < failLow)
            return new HousingHealthCheck(name, HealthLevel.Fail, $"{value:P1}");
        if (value < warnLow || value > warnHigh)
            return new HousingHealthCheck(name, HealthLevel.Warn, $"{value:P1}");
        return new HousingHealthCheck(name, HealthLevel.Pass, $"{value:P1}");
    }

    private static HousingHealthCheck High(string name, decimal value, decimal warn, decimal fail)
    {
        if (value > fail)
            return new HousingHealthCheck(name, HealthLevel.Fail, $"{value:P1}");
        if (value > warn)
            return new HousingHealthCheck(name, HealthLevel.Warn, $"{value:P1}");
        return new HousingHealthCheck(name, HealthLevel.Pass, $"{value:P1}");
    }
}

public sealed record HousingHealthCheck(string Name, HealthLevel Level, string Detail);

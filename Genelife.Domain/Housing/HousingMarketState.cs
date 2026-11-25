using Genelife.Values;

namespace Genelife.Housing;

/// <summary>
/// Housing market state shared by the market, development and admin read
/// models. It is not an ECS component; it is derived and persisted as a section
/// of the world snapshot.
/// </summary>
public sealed class HousingMarketState
{
    public decimal Index { get; set; } = 1.0m;
    public long LastUpdatedTick { get; set; }

    public Dictionary<string, int> DistrictCapacity { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> DistrictUnits { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int EvictionsThisYear { get; set; }
    public int MovesThisYear { get; set; }
    public int CompletionsThisYear { get; set; }
    public Money RentCollectedThisMonth { get; set; }
    public Money UtilityCollectedThisMonth { get; set; }
    public Money UpkeepPaidThisMonth { get; set; }
    public Money RentCollectedThisYear { get; set; }
    public Money UtilityCollectedThisYear { get; set; }
    public Money UpkeepPaidThisYear { get; set; }

    public int CapacityFor(string district)
    {
        return DistrictCapacity.TryGetValue(district, out var capacity) ? capacity : 2_000;
    }

    public int UnitsIn(string district)
    {
        return DistrictUnits.TryGetValue(district, out var units) ? units : 0;
    }

    public void AddUnits(string district, int units)
    {
        DistrictUnits[district] = UnitsIn(district) + units;
    }

    public void RemoveUnits(string district, int units)
    {
        DistrictUnits[district] = Math.Max(0, UnitsIn(district) - units);
    }

    public void ResetMonthly()
    {
        RentCollectedThisMonth = Money.Zero;
        UtilityCollectedThisMonth = Money.Zero;
        UpkeepPaidThisMonth = Money.Zero;
    }

    public void ResetYearly()
    {
        EvictionsThisYear = 0;
        MovesThisYear = 0;
        CompletionsThisYear = 0;
        RentCollectedThisYear = Money.Zero;
        UtilityCollectedThisYear = Money.Zero;
        UpkeepPaidThisYear = Money.Zero;
    }

    public void Clear()
    {
        Index = 1.0m;
        LastUpdatedTick = 0;
        DistrictCapacity.Clear();
        DistrictUnits.Clear();
        ResetMonthly();
        ResetYearly();
    }
}

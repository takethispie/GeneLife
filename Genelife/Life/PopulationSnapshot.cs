using Arch.Core;
using Genelife.Enums;

namespace Genelife.Life;

public sealed record MortalityRecord(
    long Tick,
    ulong SimNetworkId,
    string Name,
    int Age,
    DeathCause Cause,
    decimal Wealth);

public sealed record PopulationSnapshot(
    long Tick,
    int Year,
    int Total,
    int Education,
    int Work,
    int Retirement,
    int Elderly,
    int StudentsEnrolled,
    int GraduatesThisYear,
    int Employed,
    int Unemployed,
    int RetiredThisYear,
    int DeathsThisYear,
    int ArrivalsThisYear,
    decimal AverageAge,
    decimal MedianAge,
    decimal AverageHealth,
    decimal AverageAgeAtDeath,
    decimal LifeExpectancy,
    decimal PensionReplacementRate,
    decimal PensionFundingRatio,
    decimal WorkingAgeShare,
    decimal OldAgeDependencyRatio,
    IReadOnlyList<int> PyramidBuckets,
    IReadOnlyList<DeathCauseCount> DeathCauses);

public sealed record DeathCauseCount(string Cause, int Count);

public sealed class EstateRequest
{
    public Entity Sim { get; init; }
    public Entity Household { get; init; }
    public Entity OwnedUnit { get; init; }
    public Entity Unit { get; init; }
    public long Tick { get; init; }
    public string Name { get; init; } = string.Empty;
    public ulong SimNetworkId { get; init; }
    public int Age { get; init; }
    public DeathCause Cause { get; init; }
    public List<Entity> Heirs { get; init; } = new();
    public decimal HouseholdRentArrears { get; init; }
    public decimal HouseholdUtilityArrears { get; init; }
}

public sealed class PendingArrival
{
    public string Name { get; init; } = string.Empty;
    public int Age { get; init; }
    public Components.Life.SkillProfile Profile { get; init; }
    public long QueuedTick { get; init; }
    public long ExpiresTick { get; init; }
}

public sealed class ImmigrationState
{
    public int ArrivalsThisYear { get; set; }
    public int ArrivalBudget { get; set; }
    public int Year { get; set; }
    public int CancelledThisYear { get; set; }
    public bool Paused { get; set; }
    public List<PendingArrival> Pending { get; } = new();

    public void ResetYear(int year, int budget)
    {
        Year = year;
        ArrivalBudget = budget;
        ArrivalsThisYear = 0;
        CancelledThisYear = 0;
    }

    public void Clear()
    {
        ArrivalsThisYear = 0;
        ArrivalBudget = 0;
        Year = 0;
        CancelledThisYear = 0;
        Paused = false;
        Pending.Clear();
    }
}

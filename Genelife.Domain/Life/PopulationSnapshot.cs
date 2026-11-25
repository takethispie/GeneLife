using Arch.Core;
using Genelife.Enums;
using Genelife.Values;

namespace Genelife.Life;

public sealed record MortalityRecord(
    long Tick,
    ulong GenosNetworkId,
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
    IReadOnlyList<DeathCauseCount> DeathCauses,
    int BirthsThisYear = 0,
    decimal BirthRate = 0m,
    int NaturalChange = 0,
    int NetMigration = 0,
    decimal ReplacementRate = 0m,
    int Children = 0,
    int ChildrenUnderSchoolAge = 0,
    decimal AverageHouseholdSize = 0m,
    int Orphans = 0,
    int StateCareChildren = 0,
    decimal GuardianCoverage = 0m,
    Money MinorTrustTotal = default,
    Money InheritancePaidThisYear = default,
    Money TrustReleasedThisYear = default);

public sealed record DeathCauseCount(string Cause, int Count);

public sealed class EstateRequest
{
    public Entity Genos { get; init; }
    public Entity Household { get; init; }
    public Entity OwnedUnit { get; init; }
    public Entity Unit { get; init; }
    public long Tick { get; init; }
    public string Name { get; init; } = string.Empty;
    public ulong GenosNetworkId { get; init; }
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

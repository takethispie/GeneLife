namespace Genelife.Api.DTOs;

public sealed class LifecycleStatus
{
    public int TotalGenoses { get; set; }
    public int Education { get; set; }
    public int Work { get; set; }
    public int Retirement { get; set; }
    public int Elderly { get; set; }
    public decimal AverageAge { get; set; }
    public decimal MedianAge { get; set; }
    public decimal AverageHealth { get; set; }
    public decimal WorkingAgeShare { get; set; }
    public decimal OldAgeDependencyRatio { get; set; }
    public int StudentsEnrolled { get; set; }
    public int UnenrolledStudents { get; set; }
    public decimal EnrollmentRate { get; set; }
    public int GraduatesThisYear { get; set; }
    public int Employed { get; set; }
    public int Unemployed { get; set; }
    public decimal UnemploymentRate { get; set; }
    public int RetiredThisYear { get; set; }
    public int DeathsThisYear { get; set; }
    public int ArrivalsThisYear { get; set; }
    public decimal LifeExpectancy { get; set; }
    public int MortalityRecords { get; set; }
    public decimal PensionFund { get; set; }
    public decimal PensionLiabilities { get; set; }
    public decimal PensionFundingRatio { get; set; }
    public decimal AveragePension { get; set; }
    public decimal PensionReplacementRate { get; set; }
    public int Schools { get; set; }
    public StagePolicyStatus Policy { get; set; } = new();
    public ImmigrationStatus Immigration { get; set; } = new();
    public PopulationSnapshotStatus? LatestSnapshot { get; set; }
    public List<LifecycleHealthCheckStatus> Health { get; set; } = new();
}

public sealed class StagePolicyStatus
{
    public int SchoolStartAge { get; set; }
    public int WorkStartAge { get; set; }
    public int RetirementAge { get; set; }
    public int ElderlyAge { get; set; }
    public int MaxAge { get; set; }
}

public sealed class ImmigrationStatus
{
    public int TargetPopulation { get; set; }
    public bool Enabled { get; set; }
    public int ArrivalsThisYear { get; set; }
    public int ArrivalBudget { get; set; }
    public int Pending { get; set; }
    public int CancelledThisYear { get; set; }
    public bool Paused { get; set; }
}

public sealed class SchoolStatus
{
    public ulong NetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int Enrolled { get; set; }
    public decimal Attendance { get; set; }
    public decimal Quality { get; set; }
    public string Emphasis { get; set; } = string.Empty;
    public decimal MonthlyCost { get; set; }
    public ulong? OwnerNetworkId { get; set; }
    public string Owner { get; set; } = string.Empty;
}

public sealed class GenosLifecycleStatus
{
    public ulong NetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Stage { get; set; } = string.Empty;
    public long AgeTicks { get; set; }
    public long StageEnteredTick { get; set; }
    public string LastTransitionCause { get; set; } = string.Empty;
    public decimal Health { get; set; }
    public decimal SkillTotal { get; set; }
    public string TopSkill { get; set; } = string.Empty;
    public bool IsEnrolled { get; set; }
    public bool IsUnenrolled { get; set; }
    public ulong? SchoolNetworkId { get; set; }
    public string School { get; set; } = string.Empty;
    public decimal Attendance { get; set; }
    public int MonthsAttended { get; set; }
    public bool IsEmployed { get; set; }
    public string Employer { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Seniority { get; set; } = string.Empty;
    public decimal Salary { get; set; }
    public bool IsAtWork { get; set; }
    public decimal Performance { get; set; }
    public int Warnings { get; set; }
    public bool IsLookingForWork { get; set; }
    public decimal? MonthlyPension { get; set; }
    public string? RetirementFund { get; set; }
    public bool PensionReduced { get; set; }
    public long? UnemploymentCycle { get; set; }
    public bool IsDead { get; set; }
    public ulong? HouseholdNetworkId { get; set; }

    public bool HasTraits { get; set; }
    public string EyeColor { get; set; } = string.Empty;
    public string HairColor { get; set; } = string.Empty;
    public string Handedness { get; set; } = string.Empty;
    public string Morphotype { get; set; } = string.Empty;
    public string Aptitude { get; set; } = string.Empty;
    public string HeightPotential { get; set; } = string.Empty;
    public string Sex { get; set; } = string.Empty;
    public string Temperament { get; set; } = string.Empty;
}

public sealed class PensionStatus
{
    public decimal FundBalance { get; set; }
    public decimal ProjectedLiabilities { get; set; }
    public decimal FundingRatio { get; set; }
    public int Members { get; set; }
    public decimal TotalContributions { get; set; }
    public decimal AverageMonthlyPension { get; set; }
    public decimal ReplacementRate { get; set; }
    public decimal PaidThisCycle { get; set; }
    public decimal ShortfallThisCycle { get; set; }
    public int ReducedThisCycle { get; set; }
    public List<PensionMemberStatus> MemberDetail { get; set; } = new();
}

public sealed class PensionMemberStatus
{
    public ulong NetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public decimal MonthlyPension { get; set; }
    public decimal Contributions { get; set; }
    public int ContributingCycles { get; set; }
    public bool MeansTested { get; set; }
    public bool Reduced { get; set; }
}

public sealed class MortalityRecordStatus
{
    public long Tick { get; set; }
    public ulong GenosNetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Cause { get; set; } = string.Empty;
    public decimal Wealth { get; set; }
}

public sealed class PopulationSnapshotStatus
{
    public long Tick { get; set; }
    public int Year { get; set; }
    public int Total { get; set; }
    public int Education { get; set; }
    public int Work { get; set; }
    public int Retirement { get; set; }
    public int Elderly { get; set; }
    public int StudentsEnrolled { get; set; }
    public int GraduatesThisYear { get; set; }
    public int Employed { get; set; }
    public int Unemployed { get; set; }
    public int RetiredThisYear { get; set; }
    public int DeathsThisYear { get; set; }
    public int ArrivalsThisYear { get; set; }
    public decimal AverageAge { get; set; }
    public decimal MedianAge { get; set; }
    public decimal AverageHealth { get; set; }
    public decimal AverageAgeAtDeath { get; set; }
    public decimal LifeExpectancy { get; set; }
    public decimal PensionReplacementRate { get; set; }
    public decimal PensionFundingRatio { get; set; }
    public decimal WorkingAgeShare { get; set; }
    public decimal OldAgeDependencyRatio { get; set; }
    public List<int> PyramidBuckets { get; set; } = new();
    public List<DeathCauseStatus> DeathCauses { get; set; } = new();
}

public sealed class DeathCauseStatus
{
    public string Cause { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class LifecycleHealthCheckStatus
{
    public string Name { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public sealed class LifecycleOverviewResponse
{
    public LifecycleStatus Status { get; set; } = new();
    public List<SchoolStatus> Schools { get; set; } = new();
    public PensionStatus Pensions { get; set; } = new();
    public List<MortalityRecordStatus> Mortality { get; set; } = new();
    public List<PopulationSnapshotStatus> PopulationHistory { get; set; } = new();
}

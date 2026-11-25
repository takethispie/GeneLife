namespace Genelife.Messages.Events;

public sealed record GenosAged(ulong GenosNetworkId, int Age, long AgeTicks);

public sealed record MaxAgeReached(ulong GenosNetworkId, int Age);

public sealed record LifeStageChanged(
    ulong GenosNetworkId,
    string From,
    string To,
    string Cause,
    long Tick);

public sealed record HouseholdIncomeChanged(ulong HouseholdNetworkId, long Tick);

public sealed record StudentEnrolled(ulong GenosNetworkId, ulong SchoolNetworkId, long Tick);

public sealed record StudentUnenrolled(ulong GenosNetworkId, long Tick);

public sealed record SchoolCapacityReached(ulong SchoolNetworkId, int Capacity, long Tick);

public sealed record SkillMilestoneReached(ulong GenosNetworkId, string Skill, float Proficiency, long Tick);

public sealed record StudentGraduated(ulong GenosNetworkId, int SkillMask, long DesiredSalaryCents, long Tick);

public sealed record ShiftStarted(ulong GenosNetworkId, ulong OfficeNetworkId, long Tick);

public sealed record ShiftEnded(ulong GenosNetworkId, ulong OfficeNetworkId, long Tick);

public sealed record ShiftInterrupted(ulong GenosNetworkId, string Reason, long Tick);

public sealed record AttendanceRecorded(ulong GenosNetworkId, float Attendance, int ShiftsCompleted, int ShiftsExpected, long Tick);

public sealed record PerformanceReview(ulong GenosNetworkId, float Performance, int Warnings, long Tick);

public sealed record EmployeeWarned(ulong GenosNetworkId, int Warnings, float Performance, long Tick);

public sealed record PromotionGranted(ulong GenosNetworkId, string From, string To, long SalaryCents, long Tick);

public sealed record SalaryAdjusted(ulong GenosNetworkId, long PreviousSalaryCents, long SalaryCents, string Reason, long Tick);

public sealed record DemotionApplied(ulong GenosNetworkId, string From, string To, long SalaryCents, long Tick);

public sealed record EmployeeRetired(ulong GenosNetworkId, ulong CompanyNetworkId, string Position, long Tick);

public sealed record Retired(ulong GenosNetworkId, int Age, long MonthlyPensionCents, long Tick);

public sealed record PensionPaid(ulong GenosNetworkId, long AmountCents, bool FromFund, long Tick);

public sealed record PensionReduced(ulong GenosNetworkId, long DueCents, long PaidCents, long Tick);

public sealed record PensionFundTopUp(long AmountCents, long FundBalanceCents, long Tick);

public sealed record PensionAccrued(ulong GenosNetworkId, long EmployeeCents, long EmployerCents, int Cycles, long Tick);

public sealed record PensionFundYieldCredited(long AmountCents, long FundBalanceCents, long Tick);

public sealed record HealthDeclined(ulong GenosNetworkId, float Health, float DeclineRate, long Tick);

public sealed record MedicalCarePurchased(ulong GenosNetworkId, long CostCents, long Tick);

public sealed record CareNeeded(ulong GenosNetworkId, float Health, long Tick);

public sealed record HealthRestored(ulong GenosNetworkId, float Health, long Tick);

public sealed record GenosDied(
    ulong GenosNetworkId,
    string Name,
    int Age,
    string Cause,
    ulong EmployerNetworkId,
    ulong HouseholdNetworkId,
    long WealthCents,
    long Tick);

public sealed record EstateSettlementRequested(ulong GenosNetworkId, long Tick);

public sealed record EstateSettled(
    ulong GenosNetworkId,
    long AssetsCents,
    long DebtsCents,
    long FuneralCents,
    long TaxCents,
    long DistributedCents,
    long EscheatCents,
    int Heirs,
    long Tick);

public sealed record InheritancePaid(ulong GenosNetworkId, ulong HeirNetworkId, long AmountCents, long Tick);

public sealed record EscheatRecorded(ulong GenosNetworkId, long AmountCents, long Tick);

public sealed record UnemploymentStarted(ulong GenosNetworkId, long BenefitCents, int MaxCycles, bool Eligible, long Tick);

public sealed record BenefitPaid(ulong GenosNetworkId, long AmountCents, int Cycle, int MaxCycles, long Tick);

public sealed record BenefitExhausted(ulong GenosNetworkId, long Tick);

public sealed record UnemploymentEnded(ulong GenosNetworkId, string Reason, long Tick);

public sealed record ImmigrantArrived(ulong GenosNetworkId, int Age, long StartingCashCents, long Tick);

public sealed record ImmigrationPaused(string Reason, long Tick);

public sealed record ArrivalCancelled(string Name, int Age, long Tick);

public sealed record PopulationGenerated(int Total, int Education, int Work, int Retirement, int Elderly, int Schools, long Tick);

public sealed record PopulationHealthCheck(string Name, string Level, string Detail);

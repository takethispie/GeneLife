namespace Genelife.Messages.Events;

public sealed record SimAged(ulong SimNetworkId, int Age, long AgeTicks);

public sealed record MaxAgeReached(ulong SimNetworkId, int Age);

public sealed record LifeStageChanged(
    ulong SimNetworkId,
    string From,
    string To,
    string Cause,
    long Tick);

public sealed record HouseholdIncomeChanged(ulong HouseholdNetworkId, long Tick);

public sealed record StudentEnrolled(ulong SimNetworkId, ulong SchoolNetworkId, long Tick);

public sealed record StudentUnenrolled(ulong SimNetworkId, long Tick);

public sealed record SchoolCapacityReached(ulong SchoolNetworkId, int Capacity, long Tick);

public sealed record SkillMilestoneReached(ulong SimNetworkId, string Skill, float Proficiency, long Tick);

public sealed record StudentGraduated(ulong SimNetworkId, int SkillMask, long DesiredSalaryCents, long Tick);

public sealed record ShiftStarted(ulong SimNetworkId, ulong OfficeNetworkId, long Tick);

public sealed record ShiftEnded(ulong SimNetworkId, ulong OfficeNetworkId, long Tick);

public sealed record ShiftInterrupted(ulong SimNetworkId, string Reason, long Tick);

public sealed record AttendanceRecorded(ulong SimNetworkId, float Attendance, int ShiftsCompleted, int ShiftsExpected, long Tick);

public sealed record PerformanceReview(ulong SimNetworkId, float Performance, int Warnings, long Tick);

public sealed record EmployeeWarned(ulong SimNetworkId, int Warnings, float Performance, long Tick);

public sealed record PromotionGranted(ulong SimNetworkId, string From, string To, long SalaryCents, long Tick);

public sealed record SalaryAdjusted(ulong SimNetworkId, long PreviousSalaryCents, long SalaryCents, string Reason, long Tick);

public sealed record DemotionApplied(ulong SimNetworkId, string From, string To, long SalaryCents, long Tick);

public sealed record EmployeeRetired(ulong SimNetworkId, ulong CompanyNetworkId, string Position, long Tick);

public sealed record Retired(ulong SimNetworkId, int Age, long MonthlyPensionCents, long Tick);

public sealed record PensionPaid(ulong SimNetworkId, long AmountCents, bool FromFund, long Tick);

public sealed record PensionReduced(ulong SimNetworkId, long DueCents, long PaidCents, long Tick);

public sealed record PensionFundTopUp(long AmountCents, long FundBalanceCents, long Tick);

public sealed record PensionAccrued(ulong SimNetworkId, long EmployeeCents, long EmployerCents, int Cycles, long Tick);

public sealed record PensionFundYieldCredited(long AmountCents, long FundBalanceCents, long Tick);

public sealed record HealthDeclined(ulong SimNetworkId, float Health, float DeclineRate, long Tick);

public sealed record MedicalCarePurchased(ulong SimNetworkId, long CostCents, long Tick);

public sealed record CareNeeded(ulong SimNetworkId, float Health, long Tick);

public sealed record HealthRestored(ulong SimNetworkId, float Health, long Tick);

public sealed record SimDied(
    ulong SimNetworkId,
    string Name,
    int Age,
    string Cause,
    ulong EmployerNetworkId,
    ulong HouseholdNetworkId,
    long WealthCents,
    long Tick);

public sealed record EstateSettlementRequested(ulong SimNetworkId, long Tick);

public sealed record EstateSettled(
    ulong SimNetworkId,
    long AssetsCents,
    long DebtsCents,
    long FuneralCents,
    long TaxCents,
    long DistributedCents,
    long EscheatCents,
    int Heirs,
    long Tick);

public sealed record InheritancePaid(ulong SimNetworkId, ulong HeirNetworkId, long AmountCents, long Tick);

public sealed record EscheatRecorded(ulong SimNetworkId, long AmountCents, long Tick);

public sealed record UnemploymentStarted(ulong SimNetworkId, long BenefitCents, int MaxCycles, bool Eligible, long Tick);

public sealed record BenefitPaid(ulong SimNetworkId, long AmountCents, int Cycle, int MaxCycles, long Tick);

public sealed record BenefitExhausted(ulong SimNetworkId, long Tick);

public sealed record UnemploymentEnded(ulong SimNetworkId, string Reason, long Tick);

public sealed record ImmigrantArrived(ulong SimNetworkId, int Age, long StartingCashCents, long Tick);

public sealed record ImmigrationPaused(string Reason, long Tick);

public sealed record ArrivalCancelled(string Name, int Age, long Tick);

public sealed record PopulationGenerated(int Total, int Education, int Work, int Retirement, int Elderly, int Schools, long Tick);

public sealed record PopulationHealthCheck(string Name, string Level, string Detail);

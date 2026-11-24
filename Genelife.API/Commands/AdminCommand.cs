using Genelife.Enums;
using Genelife.Values;

namespace Genelife.Api.Commands;

/// <summary>
/// Typed admin mutation. Commands are enqueued by HTTP endpoints and applied at
/// the start of the next simulation tick, never on the HTTP thread.
/// </summary>
public abstract record AdminCommand(Guid CommandId, string IssuedBy);

public sealed record CreateOfficeCommand(
    Guid CommandId,
    string IssuedBy,
    ulong CompanyId,
    string Name,
    int Capacity,
    Money OperatingCost) : AdminCommand(CommandId, IssuedBy);

public sealed record UpdateOfficeCommand(
    Guid CommandId,
    string IssuedBy,
    ulong OfficeId,
    string? Name,
    int? Capacity,
    Money? OperatingCost) : AdminCommand(CommandId, IssuedBy);

public sealed record CloseOfficeCommand(
    Guid CommandId,
    string IssuedBy,
    ulong OfficeId) : AdminCommand(CommandId, IssuedBy);

public sealed record OpenJobPostingCommand(
    Guid CommandId,
    string IssuedBy,
    ulong CompanyId,
    ulong OfficeId,
    string Position,
    Money Salary,
    Skill Requirements,
    int Seats) : AdminCommand(CommandId, IssuedBy);

public sealed record CloseJobPostingCommand(
    Guid CommandId,
    string IssuedBy,
    ulong PostingId) : AdminCommand(CommandId, IssuedBy);

public sealed record UpdateJobPostingSalaryCommand(
    Guid CommandId,
    string IssuedBy,
    ulong PostingId,
    Money Salary) : AdminCommand(CommandId, IssuedBy);

public sealed record ResizeJobPostingCommand(
    Guid CommandId,
    string IssuedBy,
    ulong PostingId,
    int Seats) : AdminCommand(CommandId, IssuedBy);

public sealed record InjectCapitalCommand(
    Guid CommandId,
    string IssuedBy,
    ulong CompanyId,
    Money Amount) : AdminCommand(CommandId, IssuedBy);

public sealed record FireEmployeeCommand(
    Guid CommandId,
    string IssuedBy,
    ulong SimNetworkId) : AdminCommand(CommandId, IssuedBy);

public sealed record ResignEmployeeCommand(
    Guid CommandId,
    string IssuedBy,
    ulong SimNetworkId) : AdminCommand(CommandId, IssuedBy);

public sealed record IssueLoanCommand(
    Guid CommandId,
    string IssuedBy,
    ulong CompanyId,
    Money Amount) : AdminCommand(CommandId, IssuedBy);

public sealed record RestructureCompanyCommand(
    Guid CommandId,
    string IssuedBy,
    ulong CompanyId) : AdminCommand(CommandId, IssuedBy);

public sealed record LiquidateCompanyCommand(
    Guid CommandId,
    string IssuedBy,
    ulong CompanyId) : AdminCommand(CommandId, IssuedBy);

public sealed record CreatePropertyCommand(
    Guid CommandId,
    string IssuedBy,
    ulong OwnerId,
    string Name,
    string District,
    int Units,
    HousingTier Tier,
    Money? MonthlyRent,
    float Condition,
    float Neighbourhood) : AdminCommand(CommandId, IssuedBy);

public sealed record UpdatePropertyCommand(
    Guid CommandId,
    string IssuedBy,
    ulong PropertyId,
    string? Name,
    float? Condition,
    Money? MonthlyUpkeep) : AdminCommand(CommandId, IssuedBy);

public sealed record UpdateHousingUnitCommand(
    Guid CommandId,
    string IssuedBy,
    ulong UnitId,
    HousingTier? Tier,
    int? Capacity,
    Money? BaseRent,
    float? Condition) : AdminCommand(CommandId, IssuedBy);

public sealed record RepairPropertyCommand(
    Guid CommandId,
    string IssuedBy,
    ulong PropertyId,
    Money Amount) : AdminCommand(CommandId, IssuedBy);

public sealed record AssignHousingCommand(
    Guid CommandId,
    string IssuedBy,
    ulong HouseholdId,
    ulong UnitId) : AdminCommand(CommandId, IssuedBy);

public sealed record EvictHouseholdCommand(
    Guid CommandId,
    string IssuedBy,
    ulong HouseholdId,
    bool Immediate) : AdminCommand(CommandId, IssuedBy);

public sealed record StartConstructionCommand(
    Guid CommandId,
    string IssuedBy,
    ulong DeveloperId,
    string District,
    HousingTier Tier,
    int Units) : AdminCommand(CommandId, IssuedBy);

public sealed record PlaceBuildingCommand(
    Guid CommandId,
    string IssuedBy,
    ulong BuildingId,
    int X,
    int Y,
    int Width,
    int Height,
    int EntryX,
    int EntryY) : AdminCommand(CommandId, IssuedBy);

public sealed record RelocateBuildingCommand(
    Guid CommandId,
    string IssuedBy,
    ulong BuildingId,
    int X,
    int Y,
    int EntryX,
    int EntryY) : AdminCommand(CommandId, IssuedBy);

public sealed record CancelConstructionCommand(
    Guid CommandId,
    string IssuedBy,
    ulong ProjectId) : AdminCommand(CommandId, IssuedBy);

public sealed record SetRentIndexCommand(
    Guid CommandId,
    string IssuedBy,
    decimal Index) : AdminCommand(CommandId, IssuedBy);

public sealed record SpawnPopulationCommand(
    Guid CommandId,
    string IssuedBy,
    int Adults,
    int Children,
    decimal ExpectedMonthlyIncome,
    int ShelterBeds,
    int BasicUnits,
    int ComfortableUnits,
    int LuxuryUnits,
    int Seed) : AdminCommand(CommandId, IssuedBy);

public sealed record CreateHouseholdCommand(
    Guid CommandId,
    string IssuedBy,
    ulong HeadSimId) : AdminCommand(CommandId, IssuedBy);

public sealed record AddHouseholdMemberCommand(
    Guid CommandId,
    string IssuedBy,
    ulong HouseholdId,
    ulong SimId,
    HouseholdRole Role) : AdminCommand(CommandId, IssuedBy);

public sealed record RemoveHouseholdMemberCommand(
    Guid CommandId,
    string IssuedBy,
    ulong HouseholdId,
    ulong SimId) : AdminCommand(CommandId, IssuedBy);

public sealed record PurchaseHomeCommand(
    Guid CommandId,
    string IssuedBy,
    ulong HouseholdId,
    ulong UnitId) : AdminCommand(CommandId, IssuedBy);

public sealed record SellHomeCommand(
    Guid CommandId,
    string IssuedBy,
    ulong HouseholdId,
    ulong UnitId,
    ulong BuyerHouseholdId) : AdminCommand(CommandId, IssuedBy);

public sealed record CreateSchoolCommand(
    Guid CommandId,
    string IssuedBy,
    string Name,
    string District,
    int Capacity,
    float Quality,
    Skill Emphasis,
    Money MonthlyCost,
    ulong? OwnerId) : AdminCommand(CommandId, IssuedBy);

public sealed record UpdateSchoolCommand(
    Guid CommandId,
    string IssuedBy,
    ulong SchoolId,
    string? Name,
    int? Capacity,
    float? Quality,
    Skill? Emphasis,
    Money? MonthlyCost) : AdminCommand(CommandId, IssuedBy);

public sealed record ForceStageCommand(
    Guid CommandId,
    string IssuedBy,
    ulong SimId,
    LifeStage Stage) : AdminCommand(CommandId, IssuedBy);

public sealed record GraduateSimCommand(
    Guid CommandId,
    string IssuedBy,
    ulong SimId) : AdminCommand(CommandId, IssuedBy);

public sealed record RetireSimCommand(
    Guid CommandId,
    string IssuedBy,
    ulong SimId) : AdminCommand(CommandId, IssuedBy);

public sealed record GrantCareCommand(
    Guid CommandId,
    string IssuedBy,
    ulong SimId,
    float HealthGain) : AdminCommand(CommandId, IssuedBy);

public sealed record GeneratePopulationCommand(
    Guid CommandId,
    string IssuedBy,
    int TotalSims,
    int Seed,
    int? Adults,
    int? Children) : AdminCommand(CommandId, IssuedBy);

public sealed record SetPopulationTargetCommand(
    Guid CommandId,
    string IssuedBy,
    int TargetPopulation,
    bool Enabled) : AdminCommand(CommandId, IssuedBy);

public sealed record PromoteSimCommand(
    Guid CommandId,
    string IssuedBy,
    ulong SimId) : AdminCommand(CommandId, IssuedBy);

public sealed record SetBenefitEligibilityCommand(
    Guid CommandId,
    string IssuedBy,
    ulong SimId,
    bool Eligible) : AdminCommand(CommandId, IssuedBy);

public sealed record TopUpPensionFundCommand(
    Guid CommandId,
    string IssuedBy,
    Money Amount) : AdminCommand(CommandId, IssuedBy);

public sealed record ForceDeathCommand(
    Guid CommandId,
    string IssuedBy,
    ulong SimId,
    DeathCause Cause) : AdminCommand(CommandId, IssuedBy);

public sealed record AdvanceAgeCommand(
    Guid CommandId,
    string IssuedBy,
    ulong SimId,
    int Years) : AdminCommand(CommandId, IssuedBy);

public sealed record PauseImmigrationCommand(
    Guid CommandId,
    string IssuedBy,
    bool Paused) : AdminCommand(CommandId, IssuedBy);

public sealed record TriggerImmigrationWaveCommand(
    Guid CommandId,
    string IssuedBy,
    int Count) : AdminCommand(CommandId, IssuedBy);

public sealed record SetEnrollmentCommand(
    Guid CommandId,
    string IssuedBy,
    ulong SimId,
    ulong SchoolId,
    bool Enrolled) : AdminCommand(CommandId, IssuedBy);

public sealed record ForcePartnershipCommand(
    Guid CommandId,
    string IssuedBy,
    ulong SimAId,
    ulong SimBId,
    bool OverrideEligibility,
    string Reason) : AdminCommand(CommandId, IssuedBy);

public sealed record EndPartnershipCommand(
    Guid CommandId,
    string IssuedBy,
    ulong SimAId,
    ulong SimBId,
    string Reason) : AdminCommand(CommandId, IssuedBy);

public sealed record StartPregnancyCommand(
    Guid CommandId,
    string IssuedBy,
    ulong CarryingParentId,
    ulong OtherParentId,
    string Reason) : AdminCommand(CommandId, IssuedBy);

public sealed record CancelPregnancyCommand(
    Guid CommandId,
    string IssuedBy,
    ulong PregnancyId,
    string Reason) : AdminCommand(CommandId, IssuedBy);

public sealed record AssignGuardianCommand(
    Guid CommandId,
    string IssuedBy,
    ulong ChildId,
    ulong GuardianId,
    string Reason) : AdminCommand(CommandId, IssuedBy);

public sealed record ReleaseTrustCommand(
    Guid CommandId,
    string IssuedBy,
    ulong TrustId,
    string Reason) : AdminCommand(CommandId, IssuedBy);

public sealed record CommandResult(
    Guid CommandId,
    bool Accepted,
    string? Error,
    object? Payload,
    DateTimeOffset CompletedAt);

public sealed record SetPlayerContinuationCommand(
    Guid CommandId,
    string IssuedBy,
    string PlayerId,
    string Policy,
    string Reason) : AdminCommand(CommandId, IssuedBy);

public sealed record DetachPlayerSimCommand(
    Guid CommandId,
    string IssuedBy,
    string PlayerId,
    string Reason) : AdminCommand(CommandId, IssuedBy);

public sealed record ForcePlayerContinuationCommand(
    Guid CommandId,
    string IssuedBy,
    string PlayerId,
    string Policy,
    string Reason) : AdminCommand(CommandId, IssuedBy);

public sealed record RestockStoreCommand(
    Guid CommandId,
    string IssuedBy,
    ulong StoreNetworkId,
    string Reason) : AdminCommand(CommandId, IssuedBy);

public sealed record RepairUtilityNetworkCommand(
    Guid CommandId,
    string IssuedBy,
    string Service,
    int Units,
    string Reason) : AdminCommand(CommandId, IssuedBy);

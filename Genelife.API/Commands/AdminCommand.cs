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

public sealed record CommandResult(
    Guid CommandId,
    bool Accepted,
    string? Error,
    object? Payload,
    DateTimeOffset CompletedAt);

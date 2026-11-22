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

public sealed record CommandResult(
    Guid CommandId,
    bool Accepted,
    string? Error,
    object? Payload,
    DateTimeOffset CompletedAt);

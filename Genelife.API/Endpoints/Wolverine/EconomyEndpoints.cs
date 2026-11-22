using Genelife.Api.Auth;
using Genelife.Api.Commands;
using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Genelife.Enums;
using Genelife.Values;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace Genelife.Api.Endpoints.Wolverine;

public static class EconomyEndpoints
{
    [WolverineGet("/api/economy/overview")]
    public static EconomyStatus Overview(SimulationEngine engine) => engine.BuildEconomyStatus();

    [WolverineGet("/api/economy/ledger")]
    public static List<LedgerEntryStatus> Ledger(
        SimulationEngine engine,
        int take = 200,
        ulong? entityId = null,
        string? kind = null)
    {
        return engine.BuildRecentLedger(Math.Clamp(take, 1, 1000), entityId, kind);
    }

    [WolverineGet("/api/economy/metrics/current")]
    public static IResult CurrentMetric(SimulationEngine engine)
    {
        var history = engine.BuildMetricHistory(1);
        return history.Count == 0
            ? Results.NotFound(new { message = "No year-end snapshot yet." })
            : Results.Ok(history[^1]);
    }

    [WolverineGet("/api/economy/metrics/history")]
    public static List<EconomySnapshotStatus> History(SimulationEngine engine, int years = 20)
    {
        return engine.BuildMetricHistory(Math.Clamp(years, 1, 50));
    }

    [WolverineGet("/api/economy/fiscal")]
    public static FiscalStatus Fiscal(SimulationEngine engine) => engine.BuildFiscalStatus();

    [WolverineGet("/api/economy/companies")]
    public static List<CompanyStatus> Companies(SimulationEngine engine)
    {
        return engine.GetWorldData().Companies;
    }

    [WolverineGet("/api/offices")]
    public static List<OfficeStatus> Offices(SimulationEngine engine)
    {
        return engine.GetWorldData().Offices;
    }

    [WolverineGet("/api/job-postings")]
    public static List<JobPostingStatus> JobPostings(SimulationEngine engine)
    {
        return engine.GetWorldData().JobPostings;
    }

    [WolverineGet("/api/commands/{commandId}")]
    public static IResult GetCommand(Guid commandId, SimulationEngine engine)
    {
        var result = engine.GetCommandResult(commandId);
        return result is null
            ? Results.NotFound(new { message = "Command not found or not yet applied." })
            : Results.Ok(result);
    }

    [WolverinePost("/api/offices")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult CreateOffice(
        CreateOfficeRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Name is required." });
        if (request.Capacity <= 0)
            return Results.BadRequest(new { message = "Capacity must be positive." });

        var command = new CreateOfficeCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            request.CompanyId,
            request.Name,
            request.Capacity,
            request.OperatingCost is null
                ? engine.Economy?.Options.DefaultOfficeOperatingCost ?? Money.FromUnits(200m)
                : Money.FromUnits(request.OperatingCost.Value));
        return Enqueue(engine, command);
    }

    [WolverinePatch("/api/offices/{officeId}")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult UpdateOffice(
        ulong officeId,
        UpdateOfficeRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        var command = new UpdateOfficeCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            officeId,
            request.Name,
            request.Capacity,
            request.OperatingCost is null ? null : Money.FromUnits(request.OperatingCost.Value));
        return Enqueue(engine, command);
    }

    [WolverineDelete("/api/offices/{officeId}")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult CloseOffice(ulong officeId, SimulationEngine engine, HttpContext http)
    {
        return Enqueue(engine, new CloseOfficeCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            officeId));
    }

    [WolverinePost("/api/job-postings")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult OpenPosting(
        OpenJobPostingRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (string.IsNullOrWhiteSpace(request.Position))
            return Results.BadRequest(new { message = "Position is required." });
        if (request.Seats <= 0)
            return Results.BadRequest(new { message = "Seats must be positive." });
        if (request.Salary <= 0)
            return Results.BadRequest(new { message = "Salary must be positive." });

        var requirements = Skill.None;
        foreach (var skill in request.Requirements ?? new List<string>())
        {
            if (Enum.TryParse<Skill>(skill, ignoreCase: true, out var parsed))
                requirements |= parsed;
        }

        return Enqueue(engine, new OpenJobPostingCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            request.CompanyId,
            request.OfficeId,
            request.Position,
            Money.FromUnits(request.Salary),
            requirements,
            request.Seats));
    }

    [WolverineDelete("/api/job-postings/{postingId}")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult ClosePosting(ulong postingId, SimulationEngine engine, HttpContext http)
    {
        return Enqueue(engine, new CloseJobPostingCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            postingId));
    }

    [WolverinePatch("/api/job-postings/{postingId}/salary")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult UpdatePostingSalary(
        ulong postingId,
        SalaryRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (request.Salary <= 0)
            return Results.BadRequest(new { message = "Salary must be positive." });

        return Enqueue(engine, new UpdateJobPostingSalaryCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            postingId,
            Money.FromUnits(request.Salary)));
    }

    [WolverinePatch("/api/job-postings/{postingId}/seats")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult ResizePosting(
        ulong postingId,
        ResizeJobPostingRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (request.Seats <= 0)
            return Results.BadRequest(new { message = "Seats must be positive." });

        return Enqueue(engine, new ResizeJobPostingCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            postingId,
            request.Seats));
    }

    [WolverinePost("/api/companies/{companyId}/capital")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult InjectCapital(
        ulong companyId,
        CapitalRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (request.Amount <= 0)
            return Results.BadRequest(new { message = "Amount must be positive." });

        return Enqueue(engine, new InjectCapitalCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            companyId,
            Money.FromUnits(request.Amount)));
    }

    [WolverinePost("/api/companies/{companyId}/employees/{simId}/fire")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Fire(ulong companyId, ulong simId, SimulationEngine engine, HttpContext http)
    {
        return Enqueue(engine, new FireEmployeeCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            simId));
    }

    [WolverinePost("/api/companies/{companyId}/employees/{simId}/resign")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Resign(ulong companyId, ulong simId, SimulationEngine engine, HttpContext http)
    {
        return Enqueue(engine, new ResignEmployeeCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            simId));
    }

    [WolverinePost("/api/companies/{companyId}/loans")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult IssueLoan(
        ulong companyId,
        CapitalRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (request.Amount <= 0)
            return Results.BadRequest(new { message = "Amount must be positive." });

        return Enqueue(engine, new IssueLoanCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            companyId,
            Money.FromUnits(request.Amount)));
    }

    [WolverinePost("/api/companies/{companyId}/restructure")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Restructure(ulong companyId, SimulationEngine engine, HttpContext http)
    {
        return Enqueue(engine, new RestructureCompanyCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            companyId));
    }

    [WolverinePost("/api/companies/{companyId}/liquidate")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Liquidate(ulong companyId, SimulationEngine engine, HttpContext http)
    {
        return Enqueue(engine, new LiquidateCompanyCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            companyId));
    }

    private static IResult Enqueue(SimulationEngine engine, AdminCommand command)
    {
        var commandId = engine.EnqueueAdminCommand(command);
        return Results.Accepted($"/api/commands/{commandId}", new { commandId });
    }
}

public sealed record CreateOfficeRequest(ulong CompanyId, string Name, int Capacity, decimal? OperatingCost = null);

public sealed record UpdateOfficeRequest(string? Name, int? Capacity, decimal? OperatingCost);

public sealed record OpenJobPostingRequest(
    ulong CompanyId,
    ulong OfficeId,
    string Position,
    decimal Salary,
    List<string>? Requirements,
    int Seats = 1);

public sealed record ResizeJobPostingRequest(int Seats);

public sealed record SalaryRequest(decimal Salary);

public sealed record CapitalRequest(decimal Amount);

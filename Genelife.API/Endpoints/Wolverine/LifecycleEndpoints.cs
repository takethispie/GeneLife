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

public static class LifecycleEndpoints
{
    [WolverineGet("/api/lifecycle")]
    public static LifecycleStatus Status(SimulationEngine engine) => engine.BuildLifecycleStatus();

    [WolverineGet("/api/lifecycle/overview")]
    public static LifecycleOverviewResponse Overview(SimulationEngine engine) => engine.BuildLifecycleOverview();

    [WolverineGet("/api/lifecycle/genoses")]
    public static List<GenosLifecycleStatus> Genoses(SimulationEngine engine, int take = 500, int skip = 0, string? stage = null) =>
        engine.BuildLifecycleGenoses(take, skip, stage);

    [WolverineGet("/api/lifecycle/schools")]
    public static List<SchoolStatus> Schools(SimulationEngine engine) => engine.BuildSchools();

    [WolverineGet("/api/lifecycle/pensions")]
    public static PensionStatus Pensions(SimulationEngine engine) => engine.BuildPensions();

    [WolverineGet("/api/lifecycle/mortality")]
    public static List<MortalityRecordStatus> Mortality(SimulationEngine engine, int take = 100) =>
        engine.BuildMortality(take);

    [WolverineGet("/api/lifecycle/population")]
    public static List<PopulationSnapshotStatus> Population(SimulationEngine engine, int years = 20) =>
        engine.BuildPopulationHistory(years);

    [WolverineGet("/api/lifecycle/population/current")]
    public static IResult CurrentPopulation(SimulationEngine engine)
    {
        var history = engine.BuildPopulationHistory(1);
        return history.Count == 0
            ? Results.NotFound(new { message = "No population snapshot yet." })
            : Results.Ok(history[^1]);
    }

    [WolverineGet("/api/lifecycle/health")]
    public static List<LifecycleHealthCheckStatus> Health(SimulationEngine engine) =>
        engine.BuildLifecycleStatus().Health;

    [WolverineGet("/api/schools")]
    public static List<SchoolStatus> SchoolsAlias(SimulationEngine engine) => engine.BuildSchools();

    [WolverinePost("/api/schools")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult CreateSchoolAlias(
        CreateSchoolRequest request,
        SimulationEngine engine,
        HttpContext http) => CreateSchool(request, engine, http);

    [WolverinePatch("/api/schools/{schoolId}")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult UpdateSchoolAlias(
        ulong schoolId,
        UpdateSchoolRequest request,
        SimulationEngine engine,
        HttpContext http) => UpdateSchool(schoolId, request, engine, http);

    [WolverinePost("/api/genoses/{genosId}/stage")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult ForceStageAlias(
        ulong genosId,
        ForceStageRequest request,
        SimulationEngine engine,
        HttpContext http) => ForceStage(genosId, request, engine, http);

    [WolverinePost("/api/genoses/{genosId}/graduate")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult GraduateAlias(ulong genosId, SimulationEngine engine, HttpContext http) =>
        Graduate(genosId, engine, http);

    [WolverinePost("/api/genoses/{genosId}/retire")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult RetireAlias(ulong genosId, SimulationEngine engine, HttpContext http) =>
        Retire(genosId, engine, http);

    [WolverinePost("/api/genoses/{genosId}/advance-age")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult AdvanceAge(
        ulong genosId,
        AdvanceAgeRequest request,
        SimulationEngine engine,
        HttpContext http) =>
        Enqueue(engine, new AdvanceAgeCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            genosId,
            Math.Max(1, request.Years)));

    [WolverinePost("/api/employment/{genosId}/promote")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Promote(ulong genosId, SimulationEngine engine, HttpContext http) =>
        Enqueue(engine, new PromoteGenosCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            genosId));

    [WolverinePost("/api/lifecycle/unemployment/{genosId}/eligibility")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult SetBenefitEligibility(
        ulong genosId,
        BenefitEligibilityRequest request,
        SimulationEngine engine,
        HttpContext http) =>
        Enqueue(engine, new SetBenefitEligibilityCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            genosId,
            request.Eligible));

    [WolverinePost("/api/lifecycle/pensions/top-up")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult TopUpPension(
        PensionTopUpRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (request.Amount <= 0)
            return Results.BadRequest(new { message = "Amount must be positive." });
        return Enqueue(engine, new TopUpPensionFundCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            Money.FromUnits(request.Amount)));
    }

    [WolverinePost("/api/lifecycle/genoses/{genosId}/death")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult ForceDeath(
        ulong genosId,
        ForceDeathRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (!Enum.TryParse<DeathCause>(request.Cause, ignoreCase: true, out var cause))
            return Results.BadRequest(new { message = "Unknown death cause." });
        return Enqueue(engine, new ForceDeathCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            genosId,
            cause));
    }

    [WolverinePost("/api/lifecycle/population/pause")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult PausePopulation(SimulationEngine engine, HttpContext http) =>
        Enqueue(engine, new PauseImmigrationCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            true));

    [WolverinePost("/api/lifecycle/population/resume")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult ResumePopulation(SimulationEngine engine, HttpContext http) =>
        Enqueue(engine, new PauseImmigrationCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            false));

    [WolverinePost("/api/lifecycle/population/wave")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult TriggerWave(
        ImmigrationWaveRequest request,
        SimulationEngine engine,
        HttpContext http) =>
        Enqueue(engine, new TriggerImmigrationWaveCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            Math.Max(1, request.Count)));

    [WolverinePost("/api/lifecycle/schools")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult CreateSchool(
        CreateSchoolRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Name is required." });
        if (string.IsNullOrWhiteSpace(request.District))
            return Results.BadRequest(new { message = "District is required." });
        if (request.Capacity <= 0)
            return Results.BadRequest(new { message = "Capacity must be positive." });

        if (!TryParseEmphasis(request.Emphasis, out var emphasis))
            return Results.BadRequest(new { message = "Unknown skill emphasis." });

        return Enqueue(engine, new CreateSchoolCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            request.Name,
            request.District,
            request.Capacity,
            request.Quality ?? 0.6f,
            emphasis,
            Money.FromUnits(request.MonthlyCost ?? 50m),
            request.OwnerId));
    }

    [WolverinePatch("/api/lifecycle/schools/{schoolId}")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult UpdateSchool(
        ulong schoolId,
        UpdateSchoolRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        Skill? emphasis = null;
        if (request.Emphasis is not null)
        {
            if (!TryParseEmphasis(request.Emphasis, out var parsed))
                return Results.BadRequest(new { message = "Unknown skill emphasis." });
            emphasis = parsed;
        }

        return Enqueue(engine, new UpdateSchoolCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            schoolId,
            request.Name,
            request.Capacity,
            request.Quality,
            emphasis,
            request.MonthlyCost is null ? null : Money.FromUnits(request.MonthlyCost.Value)));
    }

    [WolverinePost("/api/schools/{schoolId}/enroll/{genosId}")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Enroll(ulong schoolId, ulong genosId, SimulationEngine engine, HttpContext http) =>
        Enqueue(engine, new SetEnrollmentCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            genosId,
            schoolId,
            true));

    [WolverineDelete("/api/schools/{schoolId}/enroll/{genosId}")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Unenroll(ulong schoolId, ulong genosId, SimulationEngine engine, HttpContext http) =>
        Enqueue(engine, new SetEnrollmentCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            genosId,
            schoolId,
            false));

    [WolverinePost("/api/lifecycle/genoses/{genosId}/stage")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult ForceStage(
        ulong genosId,
        ForceStageRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (!Enum.TryParse<LifeStage>(request.Stage, ignoreCase: true, out var stage))
            return Results.BadRequest(new { message = "Unknown life stage." });

        return Enqueue(engine, new ForceStageCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            genosId,
            stage));
    }

    [WolverinePost("/api/lifecycle/genoses/{genosId}/graduate")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Graduate(ulong genosId, SimulationEngine engine, HttpContext http) =>
        Enqueue(engine, new GraduateGenosCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            genosId));

    [WolverinePost("/api/lifecycle/genoses/{genosId}/retire")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Retire(ulong genosId, SimulationEngine engine, HttpContext http) =>
        Enqueue(engine, new RetireGenosCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            genosId));

    [WolverinePost("/api/lifecycle/genoses/{genosId}/care")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult GrantCare(
        ulong genosId,
        GrantCareRequest request,
        SimulationEngine engine,
        HttpContext http) =>
        Enqueue(engine, new GrantCareCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            genosId,
            Math.Clamp(request.HealthGain, 1f, 100f)));

    [WolverinePost("/api/population/generate")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult GeneratePopulationAlias(
        GeneratePopulationRequest request,
        SimulationEngine engine,
        HttpContext http) => Generate(request, engine, http);

    [WolverinePost("/api/lifecycle/generate")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Generate(
        GeneratePopulationRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (request.TotalGenoses <= 0)
            return Results.BadRequest(new { message = "TotalGenoses must be positive." });

        return Enqueue(engine, new GeneratePopulationCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            request.TotalGenoses,
            request.Seed,
            request.Adults,
            request.Children));
    }

    [WolverinePost("/api/lifecycle/population/target")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult SetPopulationTarget(
        PopulationTargetRequest request,
        SimulationEngine engine,
        HttpContext http) =>
        Enqueue(engine, new SetPopulationTargetCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            Math.Max(0, request.TargetPopulation),
            request.Enabled));

    private static IResult Enqueue(SimulationEngine engine, AdminCommand command)
    {
        var commandId = engine.EnqueueAdminCommand(command);
        return Results.Accepted($"/api/commands/{commandId}", new { commandId });
    }

    private static bool TryParseEmphasis(string? value, out Skill emphasis)
    {
        emphasis = Skill.None;
        if (string.IsNullOrWhiteSpace(value))
            return true;

        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse<Skill>(part, ignoreCase: true, out var parsed))
                return false;
            emphasis |= parsed;
        }
        return true;
    }
}

public sealed record CreateSchoolRequest(
    string Name,
    string District,
    int Capacity,
    float? Quality = 0.6f,
    string? Emphasis = null,
    decimal? MonthlyCost = 50m,
    ulong? OwnerId = null);

public sealed record UpdateSchoolRequest(
    string? Name = null,
    int? Capacity = null,
    float? Quality = null,
    string? Emphasis = null,
    decimal? MonthlyCost = null);

public sealed record ForceStageRequest(string Stage);

public sealed record GrantCareRequest(float HealthGain = 25f);

public sealed record GeneratePopulationRequest(
    int TotalGenoses,
    int Seed = 1,
    int? Adults = null,
    int? Children = null);

public sealed record PopulationTargetRequest(int TargetPopulation, bool Enabled = true);

public sealed record AdvanceAgeRequest(int Years = 5);

public sealed record BenefitEligibilityRequest(bool Eligible);

public sealed record PensionTopUpRequest(decimal Amount);

public sealed record ForceDeathRequest(string Cause = "Other");

public sealed record ImmigrationWaveRequest(int Count = 10);

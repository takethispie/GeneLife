using Genelife.Api.Auth;
using Genelife.Api.Commands;
using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace Genelife.Api.Endpoints.Wolverine;

/// <summary>
/// Family read model and controlled admin operations. Traits are admin-only in
/// V4a: every route requires the admin policy and no route exposes a ranking.
/// </summary>
public static class FamilyEndpoints
{
    [WolverineGet("/api/family")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static FamilyStatus Status(SimulationEngine engine) => engine.BuildFamilyStatus();

    [WolverineGet("/api/family/traits")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static TraitDistributionStatus Traits(SimulationEngine engine) => engine.BuildTraitDistribution();

    [WolverineGet("/api/family/sims")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static FamilySimPage Sims(
        SimulationEngine engine,
        int take = 100,
        int skip = 0,
        string? stage = null,
        bool? partnered = null,
        bool? hasChildren = null,
        ulong? householdId = null,
        string? name = null) =>
        engine.BuildFamilySims(take, skip, stage, partnered, hasChildren, householdId, name);

    [WolverineGet("/api/family/sims/{simId}")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Sim(SimulationEngine engine, ulong simId)
    {
        var sim = engine.BuildFamilySim(simId);
        return sim is null
            ? Results.NotFound(new { message = "Sim not found." })
            : Results.Ok(sim);
    }

    [WolverineGet("/api/family/households")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static List<HouseholdCompositionStatus> Households(SimulationEngine engine) =>
        engine.BuildHouseholdCompositions();

    [WolverineGet("/api/family/relationships")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static FamilyRelationshipPage Relationships(
        SimulationEngine engine,
        ulong? simId = null,
        string? kind = null,
        string? source = null,
        int take = 100,
        int skip = 0) =>
        engine.BuildFamilyRelationships(simId, kind, source, take, skip);

    [WolverineGet("/api/family/attraction")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static AttractionExplanationStatus Attraction(
        SimulationEngine engine,
        ulong simId,
        ulong candidateId) =>
        engine.BuildAttractionExplanation(simId, candidateId);

    [WolverineGet("/api/family/pregnancies")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static PregnancyPageStatus Pregnancies(
        SimulationEngine engine,
        bool activeOnly = false,
        int take = 500,
        int skip = 0) =>
        engine.BuildPregnancyPage(activeOnly, take, skip);

    [WolverineGet("/api/family/pregnancies/{pregnancyId}")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Pregnancy(SimulationEngine engine, ulong pregnancyId)
    {
        var pregnancy = engine.BuildPregnancy(pregnancyId);
        return pregnancy is null
            ? Results.NotFound(new { message = "Pregnancy not found." })
            : Results.Ok(pregnancy);
    }

    [WolverineGet("/api/family/children")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static ChildPageStatus Children(
        SimulationEngine engine,
        string? ageBand = null,
        ulong? guardianId = null,
        int take = 500,
        int skip = 0) =>
        engine.BuildChildPage(ageBand, guardianId, take, skip);

    [WolverineGet("/api/family/children/{childId}")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Child(SimulationEngine engine, ulong childId)
    {
        var child = engine.BuildChild(childId);
        return child is null
            ? Results.NotFound(new { message = "Child not found." })
            : Results.Ok(child);
    }

    [WolverineGet("/api/family/guardianships")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static List<GuardianshipStatusDto> Guardianships(SimulationEngine engine) =>
        engine.BuildGuardianships();

    [WolverineGet("/api/family/health")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static List<FamilyHealthCheckStatus> Health(SimulationEngine engine) =>
        engine.BuildFamilyHealth();

    [WolverinePost("/api/family/partnerships/force")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult ForcePartnership(
        ForcePartnershipRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Results.BadRequest(new { message = "A reason is required." });

        return Enqueue(engine, new ForcePartnershipCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            request.SimAId,
            request.SimBId,
            request.OverrideEligibility,
            request.Reason));
    }

    [WolverinePost("/api/family/partnerships/end")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult EndPartnership(
        EndPartnershipRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Results.BadRequest(new { message = "A reason is required." });

        return Enqueue(engine, new EndPartnershipCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            request.SimAId,
            request.SimBId,
            request.Reason));
    }

    [WolverinePost("/api/family/pregnancies")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult StartPregnancy(
        StartPregnancyRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Results.BadRequest(new { message = "A reason is required." });

        return Enqueue(engine, new StartPregnancyCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            request.CarryingParentId,
            request.OtherParentId,
            request.Reason));
    }

    [WolverinePost("/api/family/pregnancies/{pregnancyId}/cancel")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult CancelPregnancy(
        ulong pregnancyId,
        ReasonRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Results.BadRequest(new { message = "A reason is required." });

        return Enqueue(engine, new CancelPregnancyCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            pregnancyId,
            request.Reason));
    }

    [WolverinePost("/api/family/guardianships")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult AssignGuardian(
        AssignGuardianRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Results.BadRequest(new { message = "A reason is required." });

        return Enqueue(engine, new AssignGuardianCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            request.ChildId,
            request.GuardianId,
            request.Reason));
    }

    [WolverinePost("/api/family/trusts/{trustId}/release")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult ReleaseTrust(
        ulong trustId,
        ReasonRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Results.BadRequest(new { message = "A reason is required." });

        return Enqueue(engine, new ReleaseTrustCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            trustId,
            request.Reason));
    }

    private static IResult Enqueue(SimulationEngine engine, AdminCommand command)
    {
        var commandId = engine.EnqueueAdminCommand(command);
        return Results.Accepted($"/api/commands/{commandId}", new { commandId });
    }
}

public sealed record ForcePartnershipRequest(
    ulong SimAId,
    ulong SimBId,
    string Reason,
    bool OverrideEligibility = false);

public sealed record EndPartnershipRequest(ulong SimAId, ulong SimBId, string Reason);

public sealed record StartPregnancyRequest(ulong CarryingParentId, ulong OtherParentId, string Reason);

public sealed record AssignGuardianRequest(ulong ChildId, ulong GuardianId, string Reason);

public sealed record ReasonRequest(string Reason);

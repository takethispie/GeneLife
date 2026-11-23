using Genelife.Api.Auth;
using Genelife.Api.Commands;
using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace Genelife.Api.Endpoints.Wolverine;

/// <summary>Admin consumption inspector and safe repair/restock actions.</summary>
public static class ConsumptionEndpoints
{
    [WolverineGet("/api/consumption")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static ConsumptionOverviewStatus Overview(SimulationEngine engine) =>
        engine.BuildConsumptionOverview();

    [WolverineGet("/api/consumption/networks")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IReadOnlyList<UtilityNetworkStatus> Networks(SimulationEngine engine) =>
        engine.BuildConsumptionNetworks();

    [WolverineGet("/api/consumption/households")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static ConsumptionHouseholdPage Households(
        SimulationEngine engine,
        int take = 100,
        int skip = 0,
        string? level = null) =>
        engine.BuildConsumptionHouseholds(take, skip, level);

    [WolverineGet("/api/consumption/stores")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static ConsumptionStorePage Stores(
        SimulationEngine engine,
        int take = 100,
        int skip = 0) =>
        engine.BuildConsumptionStores(take, skip);

    [WolverinePost("/api/consumption/stores/{storeNetworkId}/restock")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Restock(
        ulong storeNetworkId,
        ConsumptionReasonRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Results.BadRequest(new { message = "A reason is required." });
        return Enqueue(engine, new RestockStoreCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            storeNetworkId,
            request.Reason));
    }

    [WolverinePost("/api/consumption/networks/{service}/repair")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Repair(
        string service,
        ConsumptionRepairRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Results.BadRequest(new { message = "A reason is required." });
        if (request.Units <= 0)
            return Results.BadRequest(new { message = "Units must be positive." });
        return Enqueue(engine, new RepairUtilityNetworkCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            service,
            request.Units,
            request.Reason));
    }

    private static IResult Enqueue(SimulationEngine engine, AdminCommand command)
    {
        var id = engine.EnqueueAdminCommand(command);
        return Results.Accepted($"/api/commands/{id}", new { commandId = id });
    }
}

public sealed record ConsumptionReasonRequest(string Reason);
public sealed record ConsumptionRepairRequest(int Units, string Reason);

using Genelife.Api.Auth;
using Genelife.Api.Commands;
using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace Genelife.Api.Endpoints.Wolverine;

public static class MapEndpoints
{
    [WolverineGet("/api/map")]
    public static MapStatus GetMap(SimulationManager simulationManager) =>
        simulationManager.GetMapStatus();

    [WolverineGet("/api/map/snapshot")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static IResult GetSnapshot(
        SimulationManager simulationManager,
        RequestRateLimiter rateLimiter,
        HttpContext http)
    {
        var key = http.User.Identity?.Name
            ?? http.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        if (!rateLimiter.TryAcquire($"map-snapshot:{key}", maxPerWindow: 10, window: TimeSpan.FromSeconds(1)))
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);

        return Results.Ok(simulationManager.GetMapSnapshot());
    }

    [WolverineGet("/api/map/buildings")]
    public static List<MapBuildingStatus> GetBuildings(SimulationManager simulationManager) =>
        simulationManager.GetMapStatus().BuildingList;

    [WolverineGet("/api/map/buildings/{networkId}")]
    public static IResult GetBuilding(ulong networkId, SimulationManager simulationManager)
    {
        var building = simulationManager.GetMapBuilding(networkId);
        return building is null
            ? Results.NotFound(new { message = "Building not found" })
            : Results.Ok(building);
    }

    [WolverineGet("/api/map/viewport")]
    public static IResult GetViewport(
        int minX,
        int minY,
        int maxX,
        int maxY,
        SimulationManager simulationManager)
    {
        if (minX > maxX || minY > maxY)
            return Results.BadRequest(new { message = "Invalid viewport bounds" });

        var viewport = simulationManager.GetMapViewport(minX, minY, maxX, maxY);
        return viewport is null
            ? Results.BadRequest(new { message = "Viewport is outside the map or exceeds the tile budget" })
            : Results.Ok(viewport);
    }

    [WolverineGet("/api/map/health")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static MapHealthStatus GetHealth(SimulationManager simulationManager) =>
        simulationManager.GetMapHealth();

    [WolverineGet("/api/map/metrics")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IReadOnlyList<Genelife.Systems.MapMetricsSnapshot> GetMetrics(SimulationManager simulationManager) =>
        simulationManager.GetMapMetrics();

    [WolverinePost("/api/map/buildings/{networkId}/relocate")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult RelocateBuilding(
        ulong networkId,
        RelocateBuildingRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        var command = new RelocateBuildingCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            networkId,
            request.X,
            request.Y,
            request.EntryX,
            request.EntryY);
        var commandId = engine.EnqueueAdminCommand(command);
        return Results.Accepted($"/api/commands/{commandId}", new { commandId });
    }

    [WolverinePost("/api/map/buildings/{networkId}/place")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult PlaceBuilding(
        ulong networkId,
        PlaceBuildingRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        var command = new PlaceBuildingCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            networkId,
            request.X,
            request.Y,
            request.Width,
            request.Height,
            request.EntryX,
            request.EntryY);
        var commandId = engine.EnqueueAdminCommand(command);
        return Results.Accepted($"/api/commands/{commandId}", new { commandId });
    }
}

public sealed record PlaceBuildingRequest(
    int X,
    int Y,
    int Width,
    int Height,
    int EntryX,
    int EntryY);

public sealed record RelocateBuildingRequest(
    int X,
    int Y,
    int EntryX,
    int EntryY);

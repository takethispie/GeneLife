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

    [WolverinePost("/api/map/create")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static async Task<IResult> CreateMap(
        CreateMapRequest request,
        SimulationManager simulationManager,
        IConnectionRegistry connections,
        HttpContext http)
    {
        const int minimumDimension = 64;
        const int maximumDimension = 512;
        const int maximumPopulation = 5000;

        if (request.Width < minimumDimension || request.Width > maximumDimension ||
            request.Height < minimumDimension || request.Height > maximumDimension)
        {
            return Results.BadRequest(new
            {
                message = $"Width and height must be between {minimumDimension} and {maximumDimension}."
            });
        }

        if (request.Population < 0 || request.Population > maximumPopulation)
        {
            return Results.BadRequest(new
            {
                message = $"Population must be between 0 and {maximumPopulation}."
            });
        }

        if (request.Adults is < 0 || request.Children is < 0)
            return Results.BadRequest(new { message = "Adults and children cannot be negative." });

        if ((request.Adults ?? 0) > request.Population || (request.Children ?? 0) > request.Population)
            return Results.BadRequest(new { message = "Adults and children cannot exceed the population." });

        if (simulationManager.HasMap && !request.Confirm)
        {
            return Results.Conflict(new
            {
                message = "A map already exists; confirmation is required.",
                requiresConfirmation = true
            });
        }

        var issuedBy = http.User.Identity?.Name ?? "admin";

        try
        {
            var playerIds = connections.GetPlayerIds();
            var result = await simulationManager.CreateMap(request, playerIds, issuedBy, http.RequestAborted);
            return Results.Ok(result);
        }
        catch (WorldResetConfirmationRequiredException)
        {
            return Results.Conflict(new
            {
                message = "A map already exists; confirmation is required.",
                requiresConfirmation = true
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "World creation failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

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
    public static IReadOnlyList<Genelife.Application.Systems.MapMetricsSnapshot> GetMetrics(SimulationManager simulationManager) =>
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

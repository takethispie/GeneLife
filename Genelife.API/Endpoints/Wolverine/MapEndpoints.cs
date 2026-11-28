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
        // Phase 4: the 64..512 per-side cap is replaced by a TILE BUDGET so a
        // 32000x32000 request (1.024e9 tiles) is legal but still bounded. Keep the
        // sides explicit; the product is the world, and 32000 is the ruled extent.
        const long maximumTiles = 32000L * 32000L;
        const int maximumPopulation = 5000;

        if (request.Width < minimumDimension || request.Height < minimumDimension)
        {
            return Results.BadRequest(new
            {
                message = $"Width and height must each be at least {minimumDimension} tiles."
            });
        }

        var requestedTiles = (long)request.Width * request.Height;
        if (requestedTiles > maximumTiles)
        {
            return Results.BadRequest(new
            {
                message = $"{request.Width}x{request.Height} = {requestedTiles} tiles exceeds the " +
                          $"{maximumTiles}-tile world budget."
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

    /// <summary>
    /// The world manifest — fetched once per session and cached. The snapshot
    /// protocol is versioned configuration, not a feature flag: the whole-map
    /// <c>/api/map/snapshot</c> path is retained alongside these chunk endpoints.
    /// </summary>
    [WolverineGet("/api/map/manifest")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static IResult GetManifest(SimulationManager simulationManager) =>
        Results.Ok(simulationManager.GetMapManifest());

    /// <summary>
    /// One viewport's tiles, addressed by a chunk rectangle. The client derives
    /// the chunk set from its camera rect (no round trip) and issues ONE request
    /// per viewport rather than one per chunk. Chunks carry chunk-local RLE runs;
    /// buildings intersecting the same tile rect ride the same response so tiles
    /// and entities never tear at the viewport edge.
    ///
    /// Failure semantics are defined (E-10): an inverted/out-of-grid range is a
    /// 400, no world is a 409, and a stale <paramref name="specHash"/> is a 409
    /// that tells the client to re-fetch the manifest.
    /// </summary>
    [WolverineGet("/api/map/chunks")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static IResult GetChunks(
        int cxMin,
        int cyMin,
        int cxMax,
        int cyMax,
        string? specHash,
        SimulationManager simulationManager)
    {
        var manifest = simulationManager.GetMapManifest();
        if (!manifest.HasMap)
            return Results.Conflict(new { code = "no-map", message = "No map is loaded." });

        if (specHash is not null && !string.Equals(specHash, manifest.SpecHash, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Conflict(new
            {
                code = "spec-mismatch",
                message = "The world was regenerated; re-fetch the manifest.",
                specHash = manifest.SpecHash
            });
        }

        if (cxMin > cxMax || cyMin > cyMax)
            return Results.BadRequest(new { code = "invalid-range", message = "Chunk range is inverted." });

        if (cxMin < 0 || cyMin < 0 || cxMax >= manifest.ChunksPerSideX || cyMax >= manifest.ChunksPerSideY)
        {
            return Results.BadRequest(new
            {
                code = "out-of-range",
                message = $"Chunk range ({cxMin},{cyMin})..({cxMax},{cyMax}) is outside the " +
                          $"{manifest.ChunksPerSideX}×{manifest.ChunksPerSideY} grid."
            });
        }

        var batch = simulationManager.GetMapChunks(cxMin, cyMin, cxMax, cyMax);
        return batch is null
            ? Results.Conflict(new { code = "no-map", message = "No map is loaded." })
            : Results.Ok(batch);
    }

    /// <summary>
    /// The coarse world overview raster (one dominant-terrain code per chunk).
    /// Fetched once when the map opens, on its own endpoint rather than riding the
    /// manifest, and stable for a given spec hash.
    /// </summary>
    [WolverineGet("/api/map/overview")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static IResult GetOverview(string? specHash, SimulationManager simulationManager)
    {
        var manifest = simulationManager.GetMapManifest();
        if (!manifest.HasMap)
            return Results.Conflict(new { code = "no-map", message = "No map is loaded." });

        if (specHash is not null && !string.Equals(specHash, manifest.SpecHash, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Conflict(new
            {
                code = "spec-mismatch",
                message = "The world was regenerated; re-fetch the manifest.",
                specHash = manifest.SpecHash
            });
        }

        var overview = simulationManager.GetMapOverview();
        return overview is null
            ? Results.Conflict(new { code = "no-map", message = "No map is loaded." })
            : Results.Ok(overview);
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

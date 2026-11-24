using Genelife.Api.Auth;
using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Genelife.Api.Snapshots;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace Genelife.Api.Endpoints.Wolverine;

/// <summary>
/// Simulation lifecycle and operational endpoints. These keep the pre-migration
/// routes, status codes and policies while running on the Wolverine HTTP stack.
/// </summary>
public static class SimulationEndpoints
{
    [WolverinePost("/api/simulation/start")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Start(SimulationManager simulationManager)
    {
        if (simulationManager.IsRunning)
            return Results.BadRequest(new { message = "Simulation is already running" });

        simulationManager.StartSimulation();
        return Results.Ok(new { message = "Simulation started" });
    }

    [WolverinePost("/api/simulation/stop")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Stop(SimulationManager simulationManager)
    {
        if (!simulationManager.IsRunning)
            return Results.BadRequest(new { message = "Simulation is not running" });

        simulationManager.StopSimulation();
        return Results.Ok(new { message = "Simulation stopped" });
    }

    [WolverineGet("/api/simulation/status")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static async Task<IResult> Status(SimulationManager simulationManager)
    {
        return Results.Ok(new
        {
            isRunning = simulationManager.IsRunning,
            sims = await simulationManager.GetAllSimsAsync()
        });
    }

    [WolverineGet("/api/simulation/metrics")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Metrics(
        SimulationManager simulationManager,
        SimulationEngine engine,
        SimulationLoop loop,
        ReplicationMetrics replicationMetrics)
    {
        var metrics = engine.GetMetricsSnapshot();
        var replication = replicationMetrics.Snapshot();

        return Results.Ok(new
        {
            isRunning = simulationManager.IsRunning,
            tick = engine.CurrentTick,
            loop = new
            {
                ticks = loop.TickCount,
                overruns = loop.OverrunCount,
                droppedTicks = loop.DroppedTicks,
                lastTickMs = loop.LastTickMilliseconds,
                tickBudgetMs = loop.TickBudgetMilliseconds,
                targetRatePerSecond = loop.CurrentTickRatePerSecond(),
                effectiveRatePerSecond = loop.EffectiveTickRatePerSecond()
            },
            metrics,
            replication,
            scaling = ScalingTriggers.Evaluate(metrics, replication)
        });
    }

    [WolverinePost("/api/simulation/sims")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult AddSim(AddSimRequest request, SimulationManager simulationManager)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Name is required" });

        try
        {
            simulationManager.AddSim(request.Name, request.Age);
            return Results.Ok(new { message = $"Sim '{request.Name}' added successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    [WolverineGet("/api/simulation/sims")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static Task<List<SimStatus>> GetAllSims(SimulationManager simulationManager) =>
        simulationManager.GetAllSimsAsync();

    [WolverinePost("/api/simulation/save")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Save(SimulationManager simulationManager)
    {
        if (!simulationManager.IsRunning)
            return Results.BadRequest(new { message = "Simulation is not running" });

        try
        {
            var state = simulationManager.SaveSnapshot();
            return Results.Ok(new SnapshotInfo
            {
                Tick = state.Tick,
                SchemaVersion = state.SchemaVersion,
                SimCount = state.Sims.Count,
                CompanyCount = state.Companies.Count,
                SavedAt = DateTime.UtcNow.ToString("O")
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    [WolverinePost("/api/simulation/restore")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Restore(SimulationManager simulationManager)
    {
        try
        {
            simulationManager.RestoreSnapshot();
            return Results.Ok(new { message = "Simulation restored from latest snapshot" });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }
}

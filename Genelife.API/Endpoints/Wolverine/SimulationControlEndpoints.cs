using Genelife.Api.Services;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace Genelife.Api.Endpoints.Wolverine;

public static class SimulationControlEndpoints
{
    [WolverinePost("/api/simulation/start")]
    public static IResult StartSimulation(SimulationManager simulationManager)
    {
        if (simulationManager.IsRunning)
            return Results.BadRequest(new { message = "Simulation is already running" });

        try
        {
            simulationManager.StartSimulation();
            return Results.Ok(new { message = "Simulation started" });
        }
        catch (Exception ex)
        {
            return Results.InternalServerError(new { message = ex.Message });
        }
    }

    [WolverinePost("/api/simulation/stop")]
    public static IResult StopSimulation(SimulationManager simulationManager)
    {
        if (!simulationManager.IsRunning)
            return Results.BadRequest(new { message = "Simulation is not running" });

        try
        {
            simulationManager.StopSimulation();
            return Results.Ok(new { message = "Simulation stopped" });
        }
        catch (Exception ex)
        {
            return Results.InternalServerError(new { message = ex.Message });
        }
    }

    [WolverineGet("/api/simulation/status")]
    public static object GetStatus(SimulationManager simulationManager)
    {
        return new
        {
            isRunning = simulationManager.IsRunning,
            sims = simulationManager.GetAllSims()
        };
    }
}
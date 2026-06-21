using Genelife.Api.Services;
using Genelife.Api.DTOs;

namespace Genelife.Api.Endpoints;

public static class SimulationEndpoints
{
    public static WebApplication MapSimulationEndpoints(this WebApplication app)
    {
        app.MapPost("/api/simulation/start", (SimulationManager simulationManager) =>
        {
            if (simulationManager.IsRunning)
                return Results.BadRequest(new { message = "Simulation is already running" });

            simulationManager.StartSimulation();
            return Results.Ok(new { message = "Simulation started" });
        })
        .WithName("StartSimulation");

        app.MapPost("/api/simulation/stop", (SimulationManager simulationManager) =>
        {
            if (!simulationManager.IsRunning)
                return Results.BadRequest(new { message = "Simulation is not running" });

            simulationManager.StopSimulation();
            return Results.Ok(new { message = "Simulation stopped" });
        })
        .WithName("StopSimulation");

        app.MapGet("/api/simulation/status", (SimulationManager simulationManager) =>
        {
            return Results.Ok(new
            {
                isRunning = simulationManager.IsRunning,
                sims = simulationManager.GetAllSims()
            });
        })
        .WithName("GetStatus");

        app.MapPost("/api/simulation/sims", (AddSimRequest request, SimulationManager simulationManager) =>
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
        })
        .WithName("AddSim");

        app.MapGet("/api/simulation/sims", (SimulationManager simulationManager) =>
        {
            var sims = simulationManager.GetAllSims();
            return Results.Ok(sims);
        })
        .WithName("GetAllSims");

        return app;
    }
}

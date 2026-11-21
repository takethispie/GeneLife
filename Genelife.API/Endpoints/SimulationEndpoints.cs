using Genelife.Api.Auth;
using Genelife.Api.Services;
using Genelife.Api.DTOs;

namespace Genelife.Api.Endpoints;

public static class SimulationEndpoints
{
    public static void MapSimulationEndpoints(this WebApplication app)
    {
        app.MapPost("/api/simulation/start", (SimulationManager simulationManager) =>
        {
            if (simulationManager.IsRunning)
                return Results.BadRequest(new { message = "Simulation is already running" });

            simulationManager.StartSimulation();
            return Results.Ok(new { message = "Simulation started" });
        })
        .WithName("StartSimulation")
        .RequireAuthorization(AuthPolicies.Admin);

        app.MapPost("/api/simulation/stop", (SimulationManager simulationManager) =>
        {
            if (!simulationManager.IsRunning)
                return Results.BadRequest(new { message = "Simulation is not running" });

            simulationManager.StopSimulation();
            return Results.Ok(new { message = "Simulation stopped" });
        })
        .WithName("StopSimulation")
        .RequireAuthorization(AuthPolicies.Admin);

        app.MapGet("/api/simulation/status", (SimulationManager simulationManager) =>
        {
            return Results.Ok(new
            {
                isRunning = simulationManager.IsRunning,
                sims = simulationManager.GetAllSims()
            });
        })
        .WithName("GetStatus")
        .RequireAuthorization(AuthPolicies.Player);

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
        .WithName("AddSim")
        .RequireAuthorization(AuthPolicies.Admin);

        app.MapGet("/api/simulation/sims", (SimulationManager simulationManager) =>
        {
            var sims = simulationManager.GetAllSims();
            return Results.Ok(sims);
        })
        .WithName("GetAllSims")
        .RequireAuthorization(AuthPolicies.Player);

        app.MapPost("/api/simulation/save", (SimulationManager simulationManager) =>
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
        })
        .WithName("SaveSnapshot")
        .RequireAuthorization(AuthPolicies.Admin);

        app.MapPost("/api/simulation/restore", (SimulationManager simulationManager) =>
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
        })
        .WithName("RestoreSnapshot")
        .RequireAuthorization(AuthPolicies.Admin);
    }
}

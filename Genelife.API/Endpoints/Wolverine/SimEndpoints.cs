using Genelife.Api.Services;
using Genelife.Api.DTOs;
using Genelife.Api.Models;
using Genelife.Messages.Commands;
using Genelife.Messages.Queries;
using Marten;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace Genelife.Api.Endpoints.Wolverine;

public static class SimEndpoints
{
    [WolverinePost("/api/sims")]
    public static async Task<IResult> CreateSim(CreateSim command, SimulationManager simulationManager)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Results.BadRequest(new { message = "Name is required" });

        try
        {
            var networkId = simulationManager.AddSim(command.Name, command.Age);
            return Results.Accepted("/api/sims/" + networkId, new { networkId });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    [WolverineGet("/api/sims")]
    public static Task<List<SimStatus>> GetAllSims(GetAllSims _, SimulationManager simulationManager)
    {
        return simulationManager.GetAllSimsAsync();
    }

    [WolverineGet("/api/sims/{networkId}")]
    public static async Task<IResult> GetSim(ulong networkId, SimulationManager simulationManager)
    {
        var sims = await simulationManager.GetAllSimsAsync();
        var sim = sims.FirstOrDefault(s => s.NetworkId == networkId);
        if (sim == null)
            return Results.NotFound(new { message = "Sim not found" });

        return Results.Ok(sim);
    }

    [WolverinePost("/api/sims/{networkId}/action")]
    public static IResult SetSimAction(ulong networkId, SetSimAction command, SimulationManager simulationManager)
    {
        try
        {
            simulationManager.SetSimAction(networkId, command.ActionType);
            return Results.Accepted();
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
    }

    [WolverineDelete("/api/sims/{networkId}")]
    public static IResult DeleteSim(ulong networkId, SimulationManager simulationManager)
    {
        try
        {
            var deleted = simulationManager.DeleteSim(networkId);
            if (!deleted)
                return Results.NotFound(new { message = "Sim not found" });

            return Results.Accepted();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }
}
using Genelife.Api.Services;
using Genelife.Api.DTOs;
using Genelife.Messages.Commands;
using Genelife.Messages.Queries;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace Genelife.Api.Endpoints.Wolverine;

public static class SimEndpoints
{
    [WolverinePost("/api/sims")]
    public static IResult CreateSim(CreateSim command, SimulationManager simulationManager)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Results.BadRequest(new { message = "Name is required" });

        try
        {
            simulationManager.AddSim(command.Name, command.Age);
            var sims = simulationManager.GetAllSims();
            var created = sims.LastOrDefault();
            var networkId = created?.NetworkId ?? 0UL;
            return Results.Accepted("/api/sims/" + networkId, new { networkId });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    [WolverineGet("/api/sims")]
    public static List<SimStatus> GetAllSims(GetAllSims _, SimulationManager simulationManager)
    {
        return simulationManager.GetAllSims();
    }

    [WolverineGet("/api/sims/{networkId}")]
    public static IResult GetSim(ulong networkId, SimulationManager simulationManager)
    {
        var sims = simulationManager.GetAllSims();
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
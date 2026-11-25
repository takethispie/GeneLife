using Genelife.Api.Services;
using Genelife.Api.DTOs;
using Genelife.Api.Models;
using Genelife.Messages.Commands;
using Genelife.Messages.Queries;
using Marten;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace Genelife.Api.Endpoints.Wolverine;

public static class GenosEndpoints
{
    [WolverinePost("/api/genoses")]
    public static async Task<IResult> CreateGenos(CreateGenos command, SimulationManager simulationManager)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Results.BadRequest(new { message = "Name is required" });

        try
        {
            var networkId = simulationManager.AddGenos(command.Name, command.Age);
            return Results.Accepted("/api/genoses/" + networkId, new { networkId });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    [WolverineGet("/api/genoses")]
    public static Task<List<GenosStatus>> GetAllGenoses(GetAllGenoses _, SimulationManager simulationManager)
    {
        return simulationManager.GetAllGenosesAsync();
    }

    [WolverineGet("/api/genoses/{networkId}")]
    public static async Task<IResult> GetGenos(ulong networkId, SimulationManager simulationManager)
    {
        var genoses = await simulationManager.GetAllGenosesAsync();
        var genos = genoses.FirstOrDefault(s => s.NetworkId == networkId);
        if (genos == null)
            return Results.NotFound(new { message = "Genos not found" });

        return Results.Ok(genos);
    }

    [WolverinePost("/api/genoses/{networkId}/action")]
    public static IResult SetGenosAction(ulong networkId, SetGenosAction command, SimulationManager simulationManager)
    {
        try
        {
            simulationManager.SetGenosAction(networkId, command.ActionType);
            return Results.Accepted();
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
    }

    [WolverineDelete("/api/genoses/{networkId}")]
    public static IResult DeleteGenos(ulong networkId, SimulationManager simulationManager)
    {
        try
        {
            var deleted = simulationManager.DeleteGenos(networkId);
            if (!deleted)
                return Results.NotFound(new { message = "Genos not found" });

            return Results.Accepted();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }
}
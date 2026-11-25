using Genelife.Api.Services;
using Genelife.Messages.Commands;
using Microsoft.AspNetCore.Http;

namespace Genelife.Api.Handlers;

public static class CreateGenosHandler
{
    public static async Task<IResult> Handle(CreateGenos command, SimulationManager simulationManager)
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
}
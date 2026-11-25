using Genelife.Api.Services;
using Genelife.Messages.Commands;
using Microsoft.AspNetCore.Http;

namespace Genelife.Api.Handlers;

public static class DeleteGenosHandler
{
    public static IResult Handle(DeleteGenos command, SimulationManager simulationManager)
    {
        try
        {
            simulationManager.DeleteGenos(command.NetworkId);
            return Results.Accepted();
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
    }
}
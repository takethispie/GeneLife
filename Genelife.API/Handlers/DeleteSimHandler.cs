using Genelife.Api.Services;
using Genelife.Messages.Commands;
using Microsoft.AspNetCore.Http;

namespace Genelife.Api.Handlers;

public static class DeleteSimHandler
{
    public static IResult Handle(DeleteSim command, SimulationManager simulationManager)
    {
        try
        {
            simulationManager.DeleteSim(command.NetworkId);
            return Results.Accepted();
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
    }
}
using Genelife.Api.Services;
using Genelife.Messages.Commands;
using Microsoft.AspNetCore.Http;

namespace Genelife.Api.Handlers;

public static class SetSimActionHandler
{
    public static IResult Handle(SetSimAction command, SimulationManager simulationManager)
    {
        try
        {
            simulationManager.SetSimAction(command.NetworkId, command.ActionType);
            return Results.Accepted();
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
    }
}
using Genelife.Api.Services;
using Genelife.Messages.Commands;
using Microsoft.AspNetCore.Http;

namespace Genelife.Api.Handlers;

public static class DeleteCompanyHandler
{
    public static IResult Handle(DeleteCompany command, SimulationManager simulationManager)
    {
        try
        {
            var deleted = simulationManager.DeleteCompany(command.NetworkId);
            if (!deleted)
                return Results.NotFound(new { message = "Company not found" });

            return Results.Accepted();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }
}
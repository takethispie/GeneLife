using Genelife.Api.Services;
using Genelife.Messages.Commands;
using Microsoft.AspNetCore.Http;

namespace Genelife.Api.Handlers;

public static class CreateSimHandler
{
    public static async Task<IResult> Handle(CreateSim command, SimulationManager simulationManager)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Results.BadRequest(new { message = "Name is required" });

        try
        {
            simulationManager.AddSim(command.Name, command.Age);
            var sims = await simulationManager.GetAllSimsAsync();
            var created = sims.LastOrDefault();
            var networkId = created?.NetworkId ?? 0UL;
            return Results.Accepted("/api/sims/" + networkId, new { networkId });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }
}
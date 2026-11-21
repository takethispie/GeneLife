using Genelife.Api.Auth;
using Genelife.Api.Services;

namespace Genelife.Api.Endpoints;

public static class WorldDataEndpoints
{
    public static void MapWorldDataEndpoints(this WebApplication app)
    {
        app.MapGet("/api/data", (SimulationManager simulationManager) =>
        {
            return Results.Ok(simulationManager.GetWorldData());
        })
        .WithName("GetWorldData")
        .RequireAuthorization(AuthPolicies.Player);
    }
}

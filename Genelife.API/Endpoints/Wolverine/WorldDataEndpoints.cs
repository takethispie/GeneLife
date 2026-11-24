using Genelife.Api.Auth;
using Genelife.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Wolverine.Http;

namespace Genelife.Api.Endpoints.Wolverine;

public static class WorldDataEndpoints
{
    [WolverineGet("/api/data")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static Genelife.Api.DTOs.WorldDataStatus GetWorldData(SimulationManager simulationManager) =>
        simulationManager.GetWorldData();
}

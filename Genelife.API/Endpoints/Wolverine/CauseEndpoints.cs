using Genelife.Api.Auth;
using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace Genelife.Api.Endpoints.Wolverine;

/// <summary>
/// Structured cause facts for the play surface (GDD ch.08 §8.5(2)). The server
/// sends facts only; the client owns the phrasing. The admin route is the
/// operator surface that proves the read model; the player route serves the
/// player's own genos.
/// </summary>
public static class CauseEndpoints
{
    [WolverineGet("/api/causes/{genosNetworkId:long}")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Get(ulong genosNetworkId, SimulationEngine engine)
    {
        var causes = engine.BuildCauses(genosNetworkId);
        return causes is null
            ? Results.NotFound(new { message = "Genos not found" })
            : Results.Ok(causes);
    }

    [WolverineGet("/api/causes/me")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static IResult Me(SimulationEngine engine, HttpContext http)
    {
        var playerId = http.User.GetPlayerId() ?? string.Empty;
        var genosNetworkId = engine.GetPlayerGenosNetworkId(playerId);
        if (genosNetworkId == 0)
        {
            engine.EnsurePlayerGenos(playerId);
            genosNetworkId = engine.GetPlayerGenosNetworkId(playerId);
        }

        var causes = genosNetworkId == 0 ? null : engine.BuildCauses(genosNetworkId);
        return causes is null
            ? Results.NotFound(new { message = "Cause payload is not available yet" })
            : Results.Ok(causes);
    }
}

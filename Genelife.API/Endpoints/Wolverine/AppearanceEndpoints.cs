using Genelife.Api.Auth;
using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace Genelife.Api.Endpoints.Wolverine;

/// <summary>
/// Admin appearance preview and the player's own descriptor. Appearance genes
/// and family comparison are admin-only; players receive only their own public
/// descriptor and layer contract.
/// </summary>
public static class AppearanceEndpoints
{
    [WolverineGet("/api/appearance")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static AppearancePage Page(
        SimulationEngine engine,
        int take = 100,
        int skip = 0,
        string? search = null) =>
        engine.BuildAppearancePage(take, skip, search);

    [WolverineGet("/api/appearance/me")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static IResult Me(SimulationEngine engine, HttpContext http)
    {
        if (engine.GetPlayerSimNetworkId(http.User.GetPlayerId() ?? string.Empty) == 0)
            engine.EnsurePlayerSim(http.User.GetPlayerId() ?? string.Empty);

        var status = engine.BuildOwnAppearance(http.User.GetPlayerId() ?? string.Empty);
        return status is null
            ? Results.NotFound(new { message = "Appearance is not available yet." })
            : Results.Ok(status);
    }

    [WolverineGet("/api/appearance/{simNetworkId:long}")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Get(ulong simNetworkId, SimulationEngine engine)
    {
        var status = engine.BuildAppearance(simNetworkId);
        return status is null
            ? Results.NotFound(new { message = "Sim not found." })
            : Results.Ok(status);
    }

    [WolverineGet("/api/appearance/{simNetworkId:long}/family")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Family(ulong simNetworkId, SimulationEngine engine)
    {
        var status = engine.BuildAppearanceFamily(simNetworkId);
        return status is null
            ? Results.NotFound(new { message = "Sim not found." })
            : Results.Ok(status);
    }
}

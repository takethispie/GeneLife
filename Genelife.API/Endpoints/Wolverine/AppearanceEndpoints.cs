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
        if (engine.GetPlayerGenosNetworkId(http.User.GetPlayerId() ?? string.Empty) == 0)
            engine.EnsurePlayerGenos(http.User.GetPlayerId() ?? string.Empty);

        var status = engine.BuildOwnAppearance(http.User.GetPlayerId() ?? string.Empty);
        return status is null
            ? Results.NotFound(new { message = "Appearance is not available yet." })
            : Results.Ok(status);
    }

    [WolverineGet("/api/appearance/{genosNetworkId:long}")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Get(ulong genosNetworkId, SimulationEngine engine)
    {
        var status = engine.BuildAppearance(genosNetworkId);
        return status is null
            ? Results.NotFound(new { message = "Genos not found." })
            : Results.Ok(status);
    }

    [WolverineGet("/api/appearance/{genosNetworkId:long}/family")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Family(ulong genosNetworkId, SimulationEngine engine)
    {
        var status = engine.BuildAppearanceFamily(genosNetworkId);
        return status is null
            ? Results.NotFound(new { message = "Genos not found." })
            : Results.Ok(status);
    }
}

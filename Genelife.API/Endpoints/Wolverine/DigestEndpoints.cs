using Genelife.Api.Auth;
using Genelife.Api.Digest;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace Genelife.Api.Endpoints.Wolverine;

/// <summary>
/// The Town bell digest read surface (GDD ch.08 section 8.5(6)). One pull per
/// session returns the server-computed grouping of the events touching the
/// entities a player is connected to; the operator plane gets the same grouping
/// for any player plus a whole-buffer diagnostic.
/// </summary>
public static class DigestEndpoints
{
    /// <summary>The caller's own digest — one pull per session, no push.</summary>
    [WolverineGet("/api/player/digest")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static IResult GetPlayerDigest(
        PlayerDigestScopeResolver scopes,
        TownBellDigestService digest,
        HttpContext http)
    {
        var playerId = http.User.GetPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
            return Results.Unauthorized();

        var scope = scopes.Resolve(playerId);
        return Results.Ok(digest.Build(scope, ParseWindow(http)));
    }

    /// <summary>Operator-plane: the digest for a named player.</summary>
    [WolverineGet("/api/admin/digest/{playerId}")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult GetDigestForPlayer(
        string playerId,
        PlayerDigestScopeResolver scopes,
        TownBellDigestService digest,
        HttpContext http)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            return Results.BadRequest(new { message = "A player id is required." });

        var scope = scopes.Resolve(playerId);
        return Results.Ok(digest.Build(scope, ParseWindow(http)));
    }

    /// <summary>Operator-plane: the raw projection state across every entity.</summary>
    [WolverineGet("/api/admin/digest")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult GetDigestStatus(TownBellDigestService digest) =>
        Results.Ok(digest.BuildStatus());

    private static int? ParseWindow(HttpContext http) =>
        long.TryParse(http.Request.Query["windowTicks"], out var value) && value > 0
            ? (int)Math.Min(value, int.MaxValue)
            : null;
}

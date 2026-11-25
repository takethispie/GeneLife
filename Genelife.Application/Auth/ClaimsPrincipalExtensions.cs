using System.Security.Claims;

namespace Genelife.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Returns the authenticated player's id from the <c>player_id</c> claim,
    /// falling back to the standard <c>sub</c> claim.
    /// </summary>
    public static string? GetPlayerId(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(AuthClaimTypes.PlayerId)?.Value
            ?? principal.FindFirst("sub")?.Value;
    }
}

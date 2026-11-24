using System.Security.Claims;
using Genelife.Api.Auth;

namespace Genelife.UnitTests.Auth;

public sealed class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetPlayerId_reads_player_id_claim()
    {
        var principal = CreatePrincipal(new Claim(AuthClaimTypes.PlayerId, "player-1"));

        Assert.Equal("player-1", principal.GetPlayerId());
    }

    [Fact]
    public void GetPlayerId_falls_back_to_sub_claim()
    {
        var principal = CreatePrincipal(new Claim("sub", "player-2"));

        Assert.Equal("player-2", principal.GetPlayerId());
    }

    [Fact]
    public void GetPlayerId_returns_null_when_no_player_claim_exists()
    {
        var principal = CreatePrincipal(new Claim(ClaimTypes.Name, "someone"));

        Assert.Null(principal.GetPlayerId());
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "test"));
}

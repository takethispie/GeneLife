using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Genelife.Api.Auth;
using Genelife.Api.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Genelife.UnitTests.Endpoints.Wolverine;

/// <summary>
/// The Town bell digest read surface: the player pull, the operator-plane pull
/// and their authorization policies.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DigestEndpointTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory factory;
    private readonly JwtTokenService tokenService;

    public DigestEndpointTests(TestApplicationFactory fixture)
    {
        factory = fixture;
        tokenService = factory.Services.GetRequiredService<JwtTokenService>();
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private HttpClient CreateClient(string? token = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private string AdminToken() => tokenService.CreateToken("digest-admin", isAdmin: true).Token;
    private string PlayerToken() => tokenService.CreateToken("digest-player", isAdmin: false).Token;

    [Fact]
    public async Task Player_digest_requires_authentication()
    {
        var response = await CreateClient().GetAsync("/api/player/digest", CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Player_digest_returns_a_digest_for_the_caller()
    {
        var response = await CreateClient(PlayerToken()).GetAsync("/api/player/digest", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var digest = await response.Content.ReadFromJsonAsync<TownBellDigestDto>(cancellationToken: CancellationToken);
        Assert.NotNull(digest);
        Assert.Equal("digest-player", digest!.PlayerId);
    }

    [Fact]
    public async Task Admin_digest_status_requires_the_admin_policy()
    {
        var player = await CreateClient(PlayerToken()).GetAsync("/api/admin/digest", CancellationToken);
        var admin = await CreateClient(AdminToken()).GetAsync("/api/admin/digest", CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, player.StatusCode);
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
    }

    [Fact]
    public async Task Admin_digest_for_a_named_player_honours_the_window_override()
    {
        var response = await CreateClient(AdminToken())
            .GetAsync("/api/admin/digest/digest-player?windowTicks=300", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var digest = await response.Content.ReadFromJsonAsync<TownBellDigestDto>(cancellationToken: CancellationToken);
        Assert.NotNull(digest);
        Assert.Equal("digest-player", digest!.PlayerId);
        Assert.Equal(300, digest.WindowTicks);
    }
}

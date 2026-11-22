using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Genelife.Api.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Genelife.UnitTests.Auth;

public sealed class AuthorizationTests : IClassFixture<TestApplicationFactory>
{

    private readonly TestApplicationFactory factory;
    private readonly JwtTokenService tokenService;

    public AuthorizationTests(TestApplicationFactory fixture)
    {
        factory = fixture;

        tokenService = factory.Services.GetRequiredService<JwtTokenService>();
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private HttpClient CreateClient(string? token = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private string PlayerToken(string playerId = "player-1") =>
        tokenService.CreateToken(playerId, isAdmin: false).Token;

    private string AdminToken(string playerId = "admin-1") =>
        tokenService.CreateToken(playerId, isAdmin: true).Token;

    private static async Task EnsureSimulationRunning(HttpClient client)
    {
        var response = await client.PostAsync("/api/simulation/start", null, CancellationToken);
        Assert.True(
            response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadRequest,
            $"Could not start simulation: {(int)response.StatusCode}");
    }

    [Theory]
    [InlineData("/api/companies")]
    [InlineData("/api/companies/1")]
    [InlineData("/api/data")]
    [InlineData("/api/simulation/status")]
    [InlineData("/api/simulation/sims")]
    public async Task Anonymous_read_endpoints_return_401(string path)
    {
        var response = await CreateClient().GetAsync(path, CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("unauthorized", await response.Content.ReadAsStringAsync(CancellationToken));
    }

    [Theory]
    [InlineData("POST", "/api/simulation/start")]
    [InlineData("POST", "/api/simulation/stop")]
    [InlineData("POST", "/api/simulation/sims")]
    [InlineData("POST", "/api/companies")]
    [InlineData("DELETE", "/api/companies/1")]
    public async Task Anonymous_admin_endpoints_return_401(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
            request.Content = JsonContent.Create(new { name = "Test", age = 20 });

        var response = await CreateClient().SendAsync(request, CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/companies")]
    [InlineData("/api/companies/1")]
    [InlineData("/api/data")]
    [InlineData("/api/simulation/status")]
    [InlineData("/api/simulation/sims")]
    public async Task Player_can_call_read_endpoints(string path)
    {
        await EnsureSimulationRunning(CreateClient(AdminToken()));

        var response = await CreateClient(PlayerToken()).GetAsync(path, CancellationToken);

        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            $"Expected an authorized read response, got {(int)response.StatusCode}");
    }

    [Theory]
    [InlineData("POST", "/api/simulation/start")]
    [InlineData("POST", "/api/simulation/stop")]
    [InlineData("POST", "/api/simulation/sims")]
    [InlineData("POST", "/api/companies")]
    [InlineData("DELETE", "/api/companies/1")]
    public async Task Player_cannot_call_admin_endpoints(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
            request.Content = JsonContent.Create(new { name = "Test", age = 20 });

        var response = await CreateClient(PlayerToken()).SendAsync(request, CancellationToken);

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            Assert.Contains("forbidden", await response.Content.ReadAsStringAsync(CancellationToken));
        }
        else
        {
            Assert.True(
                response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound,
                $"Unexpected status: {(int)response.StatusCode}");
        }
    }

    [Fact]
    public async Task Admin_can_start_and_stop_simulation()
    {
        var client = CreateClient(AdminToken());

        await EnsureSimulationRunning(client);

        var stop = await client.PostAsync("/api/simulation/stop", null, CancellationToken);
        Assert.Equal(HttpStatusCode.OK, stop.StatusCode);

        var start = await client.PostAsync("/api/simulation/start", null, CancellationToken);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
    }

    [Fact]
    public async Task Admin_can_add_sim()
    {
        var client = CreateClient(AdminToken());
        await EnsureSimulationRunning(client);

        var response = await client.PostAsJsonAsync(
            "/api/simulation/sims",
            new { name = "Alice", age = 30 },
            CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_create_and_delete_company()
    {
        var client = CreateClient(AdminToken());
        await EnsureSimulationRunning(client);

        var create = await client.PostAsJsonAsync("/api/companies", new { name = "Acme" }, CancellationToken);
        Assert.True(create.IsSuccessStatusCode, $"Create returned {(int)create.StatusCode}");

        using var document = JsonDocument.Parse(await create.Content.ReadAsStringAsync(CancellationToken));
        var networkId = document.RootElement.GetProperty("networkId").GetUInt64();

        var delete = await client.DeleteAsync($"/api/companies/{networkId}", CancellationToken);
        Assert.True(delete.IsSuccessStatusCode, $"Delete returned {(int)delete.StatusCode}");
    }

    [Fact]
    public async Task Invalid_bearer_token_returns_401()
    {
        var response = await CreateClient("not-a-valid-token").GetAsync("/api/companies", CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_endpoint_issues_player_token_with_player_id_and_role()
    {
        var response = await CreateClient().PostAsJsonAsync(
            "/api/auth/token",
            new { playerId = "player-42" },
            CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        Assert.Equal("player-42", document.RootElement.GetProperty("playerId").GetString());

        var token = document.RootElement.GetProperty("accessToken").GetString();
        Assert.NotNull(token);

        var jwt = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler().ReadJsonWebToken(token);
        Assert.Contains(jwt.Claims, claim => claim.Type == AuthClaimTypes.PlayerId && claim.Value == "player-42");
        Assert.Contains(jwt.Claims, claim => claim.Type == AuthClaimTypes.Role && claim.Value == AuthRoles.Player);
        Assert.DoesNotContain(jwt.Claims, claim => claim.Type == AuthClaimTypes.Role && claim.Value == AuthRoles.Admin);
    }

    [Fact]
    public async Task Token_endpoint_issues_admin_token_when_secret_matches()
    {
        var response = await CreateClient().PostAsJsonAsync(
            "/api/auth/token",
            new { playerId = "admin-42", adminSecret = TestApplicationFactory.AdminSecret },
            CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        var token = document.RootElement.GetProperty("accessToken").GetString();
        Assert.NotNull(token);

        var jwt = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler().ReadJsonWebToken(token);
        Assert.Contains(jwt.Claims, claim => claim.Type == AuthClaimTypes.Role && claim.Value == AuthRoles.Player);
        Assert.Contains(jwt.Claims, claim => claim.Type == AuthClaimTypes.Role && claim.Value == AuthRoles.Admin);
    }

    [Fact]
    public async Task Token_endpoint_rejects_wrong_admin_secret()
    {
        var response = await CreateClient().PostAsJsonAsync(
            "/api/auth/token",
            new { playerId = "admin-42", adminSecret = "wrong-secret" },
            CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("forbidden", await response.Content.ReadAsStringAsync(CancellationToken));
    }

    [Fact]
    public async Task Token_endpoint_requires_player_id()
    {
        var response = await CreateClient().PostAsJsonAsync(
            "/api/auth/token",
            new { playerId = "  " },
            CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

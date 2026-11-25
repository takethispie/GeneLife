using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Genelife.Api.Auth;
using Genelife.Messages.Commands;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Genelife.UnitTests.Endpoints.Wolverine;

[Trait("Category", "Integration")]
public sealed class WolverineEndpointsTests : IClassFixture<TestApplicationFactory>
{

    private readonly TestApplicationFactory factory;
    private readonly JwtTokenService tokenService;

    public WolverineEndpointsTests(TestApplicationFactory fixture)
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

    private string AdminToken(string playerId = "admin-1") =>
        tokenService.CreateToken(playerId, isAdmin: true).Token;

    private string PlayerToken(string playerId = "player-1") =>
        tokenService.CreateToken(playerId, isAdmin: false).Token;

    private static async Task EnsureSimulationRunning(HttpClient client)
    {
        var response = await client.PostAsync("/api/simulation/start", null, CancellationToken);
        Assert.True(
            response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadRequest,
            $"Could not start simulation: {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Wolverine_post_genoses_returns_202_with_networkId()
    {
        var client = CreateClient(AdminToken());
        await EnsureSimulationRunning(client);

        var payload = new CreateGenos("WolverineTest", 25);
        var response = await client.PostAsJsonAsync("/api/genoses", payload, CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        var networkId = document.RootElement.GetProperty("networkId").GetUInt64();
        Assert.NotEqual(0UL, networkId);
    }

    [Fact]
    public async Task Wolverine_get_genoses_returns_list()
    {
        var client = CreateClient(AdminToken());
        await EnsureSimulationRunning(client);

        var response = await client.GetAsync("/api/genoses", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var genoses = await response.Content.ReadFromJsonAsync<List<object>>(cancellationToken: CancellationToken);
        Assert.NotNull(genoses);
    }

    [Fact]
    public async Task Wolverine_genos_endpoints_require_auth()
    {
        var anonymous = CreateClient();

        var response = await anonymous.PostAsJsonAsync("/api/genoses", new CreateGenos("Test", 20), CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Wolverine_endpoints_are_primary_for_all_operations()
    {
        var client = CreateClient(AdminToken());
        await EnsureSimulationRunning(client);

        var tokenResponse = await client.PostAsJsonAsync(
            "/api/auth/token",
            new { playerId = "test-user", adminSecret = "integration-test-admin-secret" },
            CancellationToken);
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);

        var startResponse = await client.PostAsync("/api/simulation/start", null, CancellationToken);
        Assert.True(
            startResponse.IsSuccessStatusCode || startResponse.StatusCode == HttpStatusCode.BadRequest,
            $"Start returned {(int)startResponse.StatusCode}");

        var statusResponse = await client.GetAsync("/api/simulation/status", CancellationToken);
        Assert.True(
            statusResponse.IsSuccessStatusCode || statusResponse.StatusCode == HttpStatusCode.Unauthorized,
            $"Status returned {(int)statusResponse.StatusCode}");
    }

    [Fact]
    public async Task Wolverine_post_companies_returns_202()
    {
        var client = CreateClient(AdminToken());
        await EnsureSimulationRunning(client);

        var payload = new CreateCompany("WolverineCorp");
        var response = await client.PostAsJsonAsync("/api/companies", payload, CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        Assert.True(document.RootElement.TryGetProperty("networkId", out _));
    }

    [Fact]
    public async Task Wolverine_get_companies_returns_list()
    {
        var client = CreateClient(AdminToken());
        await EnsureSimulationRunning(client);

        var response = await client.GetAsync("/api/companies", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var companies = await response.Content.ReadFromJsonAsync<List<object>>(cancellationToken: CancellationToken);
        Assert.NotNull(companies);
    }

    [Fact]
    public async Task Wolverine_get_company_by_id_returns_company()
    {
        var client = CreateClient(AdminToken());
        await EnsureSimulationRunning(client);

        var createPayload = new CreateCompany("FindMeCorp");
        var createResponse = await client.PostAsJsonAsync("/api/companies", createPayload, CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, createResponse.StatusCode);

        using var createDoc = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(CancellationToken));
        var networkId = createDoc.RootElement.GetProperty("networkId").GetUInt64();

        var getResponse = await client.GetAsync($"/api/companies/{networkId}", CancellationToken);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }
}
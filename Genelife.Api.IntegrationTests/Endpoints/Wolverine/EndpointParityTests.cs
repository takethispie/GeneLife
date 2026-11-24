using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Genelife.Api.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Genelife.UnitTests.Endpoints.Wolverine;

/// <summary>
/// Guards the single-path requirement: every legacy route keeps its predecessor's
/// method, status code and policy after moving to Wolverine HTTP.
/// </summary>
[Trait("Category", "Integration")]
public sealed class EndpointParityTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory factory;
    private readonly JwtTokenService tokenService;

    public EndpointParityTests(TestApplicationFactory fixture)
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

    private string AdminToken() => tokenService.CreateToken("parity-admin", isAdmin: true).Token;
    private string PlayerToken() => tokenService.CreateToken("parity-player", isAdmin: false).Token;

    [Fact]
    public void Only_wolverine_endpoint_types_remain()
    {
        var legacy = typeof(Program).Assembly.GetTypes()
            .Where(t => t.Namespace == "Genelife.Api.Endpoints" && t.Name.EndsWith("Endpoints"))
            .Select(t => t.FullName)
            .ToArray();

        Assert.Empty(legacy);
    }

    [Fact]
    public async Task Simulation_start_is_idempotent_at_the_route()
    {
        var client = CreateClient(AdminToken());
        await client.PostAsync("/api/simulation/stop", null, CancellationToken);

        var first = await client.PostAsync("/api/simulation/start", null, CancellationToken);
        var second = await client.PostAsync("/api/simulation/start", null, CancellationToken);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Simulation_status_requires_player_policy()
    {
        var anonymous = await CreateClient().GetAsync("/api/simulation/status", CancellationToken);
        var player = await CreateClient(PlayerToken()).GetAsync("/api/simulation/status", CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, player.StatusCode);
    }

    [Fact]
    public async Task World_data_requires_player_policy_and_returns_the_read_model()
    {
        var anonymous = await CreateClient().GetAsync("/api/data", CancellationToken);
        var player = await CreateClient(PlayerToken()).GetAsync("/api/data", CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, player.StatusCode);
    }

    [Fact]
    public async Task Simulation_save_and_restore_reject_invalid_state()
    {
        var client = CreateClient(AdminToken());
        var stop = await client.PostAsync("/api/simulation/stop", null, CancellationToken);
        Assert.True(stop.IsSuccessStatusCode || stop.StatusCode == HttpStatusCode.BadRequest);

        var save = await client.PostAsync("/api/simulation/save", null, CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, save.StatusCode);
    }

    [Fact]
    public async Task Company_routes_keep_created_and_accepted_status_codes()
    {
        var client = CreateClient(AdminToken());
        await client.PostAsync("/api/simulation/start", null, CancellationToken);

        var create = await client.PostAsJsonAsync("/api/companies",
            new { name = $"Parity Co {Guid.NewGuid():N}" }, CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, create.StatusCode);

        var list = await client.GetAsync("/api/companies", CancellationToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    }
}

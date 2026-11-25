using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Genelife.Api.Auth;
using Genelife.Api.DTOs;
using Genelife.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Genelife.UnitTests.Endpoints.Wolverine;

[Trait("Category", "Integration")]
public sealed class MapCreationEndpointTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory factory;
    private readonly JwtTokenService tokenService;

    public MapCreationEndpointTests(TestApplicationFactory fixture)
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

    private string AdminToken() => tokenService.CreateToken("admin-1", isAdmin: true).Token;
    private string PlayerToken() => tokenService.CreateToken("player-1", isAdmin: false).Token;

    private static CreateMapRequest Request(bool confirm, int seed = 12, int population = 0) =>
        new(seed, 128, 128, population, seed, null, null, confirm);

    private static async Task EnsureSimulationRunning(HttpClient client)
    {
        var response = await client.PostAsync("/api/simulation/start", null, CancellationToken);
        Assert.True(
            response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadRequest,
            $"Could not start simulation: {(int)response.StatusCode}");
    }

    private static async Task EnsureSimulationStopped(HttpClient client)
    {
        var response = await client.PostAsync("/api/simulation/stop", null, CancellationToken);
        Assert.True(
            response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadRequest,
            $"Could not stop simulation: {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Create_map_requires_admin()
    {
        var anonymous = CreateClient();
        var anonymousResponse = await anonymous.PostAsJsonAsync("/api/map/create", Request(confirm: false), CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        var player = CreateClient(PlayerToken());
        var playerResponse = await player.PostAsJsonAsync("/api/map/create", Request(confirm: false), CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, playerResponse.StatusCode);
    }

    [Fact]
    public async Task Invalid_dimensions_and_population_are_rejected()
    {
        var client = CreateClient(AdminToken());

        var tooSmall = await client.PostAsJsonAsync(
            "/api/map/create",
            new CreateMapRequest(1, 32, 128, 0, 1, null, null, false),
            CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, tooSmall.StatusCode);

        var tooMany = await client.PostAsJsonAsync(
            "/api/map/create",
            new CreateMapRequest(1, 128, 128, 9000, 1, null, null, false),
            CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
    }

    [Fact]
    public async Task Existing_map_requires_confirmation()
    {
        var client = CreateClient(AdminToken());
        await EnsureSimulationRunning(client);

        var first = await client.PostAsJsonAsync("/api/map/create", Request(confirm: true, seed: 21), CancellationToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var unconfirmed = await client.PostAsJsonAsync("/api/map/create", Request(confirm: false, seed: 22), CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, unconfirmed.StatusCode);

        using var document = JsonDocument.Parse(await unconfirmed.Content.ReadAsStringAsync(CancellationToken));
        Assert.True(document.RootElement.GetProperty("requiresConfirmation").GetBoolean());

        var map = await client.GetFromJsonAsync<MapStatus>("/api/map", CancellationToken);
        Assert.NotNull(map);
        Assert.True(map!.HasMap);
        Assert.Equal(21, map.Seed);
    }

    [Fact]
    public async Task Confirmed_create_returns_result_and_updates_read_models()
    {
        var client = CreateClient(AdminToken());
        await EnsureSimulationRunning(client);

        var response = await client.PostAsJsonAsync(
            "/api/map/create",
            Request(confirm: true, seed: 31, population: 30),
            CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateMapResult>(cancellationToken: CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(31, result!.Seed);
        Assert.Equal(128, result.Width);
        Assert.Equal(128, result.Height);
        Assert.True(result.Buildings > 0);
        Assert.True(result.Population > 0);

        var map = await client.GetFromJsonAsync<MapStatus>("/api/map", CancellationToken);
        Assert.NotNull(map);
        Assert.True(map!.HasMap);
        Assert.Equal(31, map.Seed);

        var world = await client.GetFromJsonAsync<WorldDataStatus>("/api/data", CancellationToken);
        Assert.NotNull(world);
        Assert.True(world!.IsRunning);
        Assert.True(world.Sims.Count >= result.Population);
    }

    [Fact]
    public async Task Create_map_without_an_existing_map_skips_confirmation()
    {
        var client = CreateClient(AdminToken());
        await EnsureSimulationRunning(client);
        await EnsureSimulationStopped(client);

        var response = await client.PostAsJsonAsync(
            "/api/map/create",
            Request(confirm: false, seed: 41),
            CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateMapResult>(cancellationToken: CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(41, result!.Seed);

        var status = await client.GetAsync("/api/simulation/status", CancellationToken);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
    }
}

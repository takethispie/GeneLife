using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Genelife.Api.Auth;
using Genelife.Api.DTOs;
using Genelife.Api.Snapshots;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Genelife.UnitTests.Hubs;

public sealed class WorldHubTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncDisposable
{
    private const string TestKey = "integration-test-signing-key-0123456789abcdef";
    private const string TestAdminSecret = "integration-test-admin-secret";

    private readonly WebApplicationFactory<Program> factory;
    private readonly JwtTokenService tokenService;
    private readonly List<HubConnection> connections = new();

    public WorldHubTests(WebApplicationFactory<Program> fixture)
    {
        factory = fixture.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:Jwt:Issuer"] = "genelife-tests",
                    ["Auth:Jwt:Audience"] = "genelife-tests",
                    ["Auth:Jwt:Key"] = TestKey,
                    ["Auth:Jwt:AdminSecret"] = TestAdminSecret,
                    ["Auth:Jwt:TokenLifetimeMinutes"] = "60",
                    ["Logging:LogLevel:Default"] = "Warning",
                    ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning"
                });
            });
        });

        tokenService = factory.Services.GetRequiredService<JwtTokenService>();
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private string PlayerToken(string playerId = "player-1") =>
        tokenService.CreateToken(playerId, isAdmin: false).Token;

    private string AdminToken(string playerId = "admin-1") =>
        tokenService.CreateToken(playerId, isAdmin: true).Token;

    private HubConnection CreateConnection(string token, string playerId = "player-1")
    {
        var server = factory.Server;
        var url = server.BaseAddress.ToString().TrimEnd('/') + "/hubs/world";

        var connection = new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(token)!;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
            })
            .WithAutomaticReconnect()
            .Build();

        connections.Add(connection);
        return connection;
    }

    private async Task<(HttpClient, string)> StartSimulation()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AdminToken());

        await client.PostAsync("/api/simulation/start", null, CancellationToken);
        return (client, AdminToken());
    }

    [Fact]
    public async Task Connect_receives_initial_snapshot_and_disconnects_cleanly()
    {
        await StartSimulation();

        var token = PlayerToken("player-connect");
        var connection = CreateConnection(token, "player-connect");

        WorldSnapshot? received = null;
        var snapshotTcs = new TaskCompletionSource<WorldSnapshot>();

        connection.On<WorldSnapshot>("SendSnapshot", snapshot =>
        {
            received = snapshot;
            snapshotTcs.TrySetResult(snapshot);
        });

        await connection.StartAsync(CancellationToken);

        var snapshot = await snapshotTcs.Task.WaitAsync(
            TimeSpan.FromSeconds(5), CancellationToken);

        var connectionId = connection.ConnectionId!;

        Assert.NotNull(snapshot);
        Assert.True(snapshot.Tick >= 0);
        Assert.NotNull(snapshot.Sims);
        Assert.NotNull(snapshot.Companies);

        await connection.StopAsync(CancellationToken);

        await Task.Delay(200, CancellationToken);

        var registry = factory.Services.GetRequiredService<IConnectionRegistry>();
        Assert.False(registry.TryGetPlayerId(connectionId, out _));
    }

    [Fact]
    public async Task Anonymous_connection_is_rejected()
    {
        await StartSimulation();

        var server = factory.Server;
        var url = server.BaseAddress.ToString().TrimEnd('/') + "/hubs/world";

        var connection = new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
            })
            .Build();

        connections.Add(connection);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => connection.StartAsync(CancellationToken));

        Assert.Contains("401", ex.Message);
    }

    [Fact]
    public async Task SubmitIntent_with_valid_payload_succeeds()
    {
        var (client, _) = await StartSimulation();
        await client.PostAsJsonAsync("/api/simulation/sims",
            new { name = "TestSim", age = 25 }, CancellationToken);

        var token = PlayerToken("player-intent");
        var connection = CreateConnection(token, "player-intent");

        var tcs = new TaskCompletionSource<WorldSnapshot>();
        connection.On<WorldSnapshot>("SendSnapshot", snapshot => tcs.TrySetResult(snapshot));

        await connection.StartAsync(CancellationToken);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

        var payload = new
        {
            type = "move",
            playerId = "player-intent",
            x = 10.0f,
            y = 20.0f
        };

        var result = await connection.InvokeAsync<JsonElement>(
            "SubmitIntent", payload, CancellationToken);

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.True(result.GetProperty("error").ValueKind == JsonValueKind.Null);

        await connection.StopAsync(CancellationToken);
    }

    [Fact]
    public async Task SubmitIntent_with_malformed_payload_is_rejected()
    {
        await StartSimulation();

        var token = PlayerToken("player-badpayload");
        var connection = CreateConnection(token, "player-badpayload");

        var tcs = new TaskCompletionSource<WorldSnapshot>();
        connection.On<WorldSnapshot>("SendSnapshot", _ => tcs.TrySetResult(_));

        await connection.StartAsync(CancellationToken);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

        var result = await connection.InvokeAsync<JsonElement>(
            "SubmitIntent", "not-an-object", CancellationToken);

        Assert.False(result.GetProperty("success").GetBoolean());
        Assert.Equal("Payload must be a JSON object",
            result.GetProperty("error").GetString());

        await connection.StopAsync(CancellationToken);
    }

    [Fact]
    public async Task SubmitIntent_with_missing_playerid_is_rejected()
    {
        await StartSimulation();

        var token = PlayerToken("player-noid");
        var connection = CreateConnection(token, "player-noid");

        var tcs = new TaskCompletionSource<WorldSnapshot>();
        connection.On<WorldSnapshot>("SendSnapshot", _ => tcs.TrySetResult(_));

        await connection.StartAsync(CancellationToken);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

        var payload = new { type = "move" };

        var result = await connection.InvokeAsync<JsonElement>(
            "SubmitIntent", payload, CancellationToken);

        Assert.False(result.GetProperty("success").GetBoolean());
        Assert.Equal("Intent missing PlayerId",
            result.GetProperty("error").GetString());

        await connection.StopAsync(CancellationToken);
    }

    [Fact]
    public async Task SubmitIntent_with_wrong_playerid_is_rejected()
    {
        await StartSimulation();

        var token = PlayerToken("player-a");
        var connection = CreateConnection(token, "player-a");

        var tcs = new TaskCompletionSource<WorldSnapshot>();
        connection.On<WorldSnapshot>("SendSnapshot", _ => tcs.TrySetResult(_));

        await connection.StartAsync(CancellationToken);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

        var payload = new { type = "move", playerId = "player-b", x = 1.0f, y = 2.0f };

        var result = await connection.InvokeAsync<JsonElement>(
            "SubmitIntent", payload, CancellationToken);

        Assert.False(result.GetProperty("success").GetBoolean());
        Assert.Equal("PlayerId mismatch",
            result.GetProperty("error").GetString());

        await connection.StopAsync(CancellationToken);
    }

    [Fact]
    public async Task SubmitIntent_with_missing_type_is_rejected()
    {
        await StartSimulation();

        var token = PlayerToken("player-notype");
        var connection = CreateConnection(token, "player-notype");

        var tcs = new TaskCompletionSource<WorldSnapshot>();
        connection.On<WorldSnapshot>("SendSnapshot", _ => tcs.TrySetResult(_));

        await connection.StartAsync(CancellationToken);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

        var payload = new { playerId = "player-notype" };

        var result = await connection.InvokeAsync<JsonElement>(
            "SubmitIntent", payload, CancellationToken);

        Assert.False(result.GetProperty("success").GetBoolean());
        Assert.Contains("Malformed payload",
            result.GetProperty("error").GetString());

        await connection.StopAsync(CancellationToken);
    }

    [Fact]
    public async Task Rate_limit_blocks_excessive_requests()
    {
        await StartSimulation();

        var token = PlayerToken("player-ratelimit");
        var connection = CreateConnection(token, "player-ratelimit");

        var tcs = new TaskCompletionSource<WorldSnapshot>();
        connection.On<WorldSnapshot>("SendSnapshot", _ => tcs.TrySetResult(_));

        await connection.StartAsync(CancellationToken);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

        var payload = new { type = "move", playerId = "player-ratelimit", x = 1.0f, y = 2.0f };

        for (int i = 0; i < 100; i++)
        {
            var r = await connection.InvokeAsync<JsonElement>(
                "SubmitIntent", payload, CancellationToken);
            Assert.True(r.GetProperty("success").GetBoolean(),
                $"Intent {i} should succeed, got: {r.GetProperty("error")}");
        }

        var blocked = await connection.InvokeAsync<JsonElement>(
            "SubmitIntent", payload, CancellationToken);

        Assert.False(blocked.GetProperty("success").GetBoolean());
        Assert.Contains("Rate limit exceeded",
            blocked.GetProperty("error").GetString());

        await connection.StopAsync(CancellationToken);
    }

    [Fact]
    public async Task Connect_same_player_twice_both_receive_snapshots()
    {
        await StartSimulation();

        var token = PlayerToken("player-dual");

        var tcs1 = new TaskCompletionSource<WorldSnapshot>();
        var conn1 = CreateConnection(token, "player-dual");
        conn1.On<WorldSnapshot>("SendSnapshot", s => tcs1.TrySetResult(s));

        var tcs2 = new TaskCompletionSource<WorldSnapshot>();
        var conn2 = CreateConnection(token, "player-dual");
        conn2.On<WorldSnapshot>("SendSnapshot", s => tcs2.TrySetResult(s));

        await conn1.StartAsync(CancellationToken);
        await conn2.StartAsync(CancellationToken);

        var s1 = await tcs1.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        var s2 = await tcs2.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

        Assert.Equal(s1.Tick, s2.Tick);

        await conn1.StopAsync(CancellationToken);
        await conn2.StopAsync(CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var connection in connections)
        {
            try
            {
                if (connection.State == HubConnectionState.Connected)
                    await connection.StopAsync();
            }
            catch { }
            finally
            {
                await connection.DisposeAsync();
            }
        }

        connections.Clear();
    }
}
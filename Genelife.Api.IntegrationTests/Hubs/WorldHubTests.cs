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

[Trait("Category", "Integration")]
public sealed class WorldHubTests : IClassFixture<TestApplicationFactory>, IAsyncDisposable
{

    private readonly TestApplicationFactory factory;
    private readonly JwtTokenService tokenService;
    private readonly List<HubConnection> connections = new();

    public WorldHubTests(TestApplicationFactory fixture)
    {
        factory = fixture;

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
    public async Task Connect_receives_a_run_length_encoded_map_snapshot()
    {
        await StartSimulation();

        var token = PlayerToken("player-map");
        var connection = CreateConnection(token, "player-map");
        var mapTcs = new TaskCompletionSource<MapSnapshot>();
        var snapshotTcs = new TaskCompletionSource<WorldSnapshot>();

        connection.On<MapSnapshot>("SendMapSnapshot", map => mapTcs.TrySetResult(map));
        connection.On<WorldSnapshot>("SendSnapshot", snapshot => snapshotTcs.TrySetResult(snapshot));

        await connection.StartAsync(CancellationToken);

        var map = await mapTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        await snapshotTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

        Assert.NotEmpty(map.Tiles);
        Assert.Equal(map.Width * map.Height, map.Tiles.Sum(run => run.Length));
        Assert.Equal("world", map.MapId);

        await connection.StopAsync(CancellationToken);
    }

    [Fact]
    public async Task Connect_receives_welcome_with_the_players_own_sim_id()
    {
        await StartSimulation();

        var token = PlayerToken("player-welcome");
        var connection = CreateConnection(token, "player-welcome");
        var welcomeTcs = new TaskCompletionSource<WelcomeMessage>();
        var snapshotTcs = new TaskCompletionSource<WorldSnapshot>();

        connection.On<WelcomeMessage>("Welcome", welcome => welcomeTcs.TrySetResult(welcome));
        connection.On<WorldSnapshot>("SendSnapshot", snapshot => snapshotTcs.TrySetResult(snapshot));

        await connection.StartAsync(CancellationToken);

        var welcome = await welcomeTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        await snapshotTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

        Assert.Equal("player-welcome", welcome.PlayerId);
        Assert.True(welcome.SimNetworkId > 0);
        Assert.Equal("world", welcome.MapId);
        Assert.True(welcome.MapVersion >= 1);
        Assert.Equal(40, welcome.TickIntervalMs);
        Assert.Equal(WelcomeMessage.CurrentProtocolVersion, welcome.ProtocolVersion);

        await connection.StopAsync(CancellationToken);
    }

    [Fact]
    public async Task Post_admission_move_rejection_is_sent_to_the_player()
    {
        await StartSimulation();

        var token = PlayerToken("player-rejected");
        var connection = CreateConnection(token, "player-rejected");
        var welcomeTcs = new TaskCompletionSource<WelcomeMessage>();
        var snapshotTcs = new TaskCompletionSource<WorldSnapshot>();
        var rejectedTcs = new TaskCompletionSource<IntentRejected>();

        connection.On<WelcomeMessage>("Welcome", welcome => welcomeTcs.TrySetResult(welcome));
        connection.On<WorldSnapshot>("SendSnapshot", snapshot => snapshotTcs.TrySetResult(snapshot));
        connection.On<IntentRejected>("IntentRejected", rejected => rejectedTcs.TrySetResult(rejected));

        await connection.StartAsync(CancellationToken);
        var welcome = await welcomeTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        await snapshotTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

        var payload = new
        {
            type = "move",
            playerId = "player-rejected",
            sequence = 1L,
            simNetworkId = welcome.SimNetworkId,
            targetTileX = 99_999,
            targetTileY = 99_999
        };

        var result = await connection.InvokeAsync<JsonElement>(
            "SubmitIntent", payload, CancellationToken);
        Assert.True(result.GetProperty("success").GetBoolean());

        var rejected = await rejectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        Assert.Equal("player-rejected", rejected.PlayerId);
        Assert.Equal(1, rejected.Sequence);
        Assert.Equal("out_of_bounds", rejected.Code);
        Assert.False(string.IsNullOrWhiteSpace(rejected.Message));

        await connection.StopAsync(CancellationToken);
    }

    [Fact]
    public async Task SetViewport_clamps_the_radius_and_returns_the_effective_viewport()
    {
        await StartSimulation();

        var token = PlayerToken("player-viewport");
        var connection = CreateConnection(token, "player-viewport");
        var snapshotTcs = new TaskCompletionSource<WorldSnapshot>();
        connection.On<WorldSnapshot>("SendSnapshot", snapshot => snapshotTcs.TrySetResult(snapshot));

        await connection.StartAsync(CancellationToken);
        await snapshotTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

        var result = await connection.InvokeAsync<JsonElement>(
            "SetViewport",
            new { centerX = 0, centerY = 0, radius = 1 },
            CancellationToken);

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal(4, result.GetProperty("radius").GetInt32());
        Assert.Equal(0, result.GetProperty("centerX").GetInt32());
        Assert.Equal(0, result.GetProperty("centerY").GetInt32());

        await connection.StopAsync(CancellationToken);
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
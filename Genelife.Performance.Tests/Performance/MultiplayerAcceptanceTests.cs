using System.Net.Http.Json;
using System.Text.Json;
using Genelife.Api.Auth;
using Genelife.Api.DTOs;
using Genelife.Api.Snapshots;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Genelife.UnitTests.Performance;

/// <summary>
/// Wave 5 / MP14 acceptance harness: authenticated clients connect to one world,
/// receive snapshots, submit intents, resume reconnects with deltas, and survive
/// a save/stop/restore cycle.
/// </summary>
[Trait("Category", "Performance")]
public sealed class MultiplayerAcceptanceTests : IClassFixture<TestApplicationFactory>, IAsyncDisposable
{
    private const int PlayerCount = 32;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private readonly TestApplicationFactory factory;
    private readonly JwtTokenService tokenService;
    private readonly List<HubConnection> connections = new();

    public MultiplayerAcceptanceTests(TestApplicationFactory fixture)
    {
        factory = fixture;

        tokenService = factory.Services.GetRequiredService<JwtTokenService>();
    }

    private string PlayerToken(string playerId) => tokenService.CreateToken(playerId, isAdmin: false).Token;
    private string AdminToken() => tokenService.CreateToken("admin-acceptance", isAdmin: true).Token;

    private async Task<HttpClient> StartSimulationAsync()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AdminToken());

        var response = await client.PostAsync("/api/simulation/start", null, CancellationToken);
        if (response.StatusCode is not System.Net.HttpStatusCode.OK
            and not System.Net.HttpStatusCode.BadRequest)
        {
            response.EnsureSuccessStatusCode();
        }

        return client;
    }

    private static async Task AddSimsAsync(HttpClient client, int count, CancellationToken ct)
    {
        for (var i = 0; i < count; i++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/simulation/sims",
                new { name = $"AcceptanceSim_{i}", age = 25 },
                ct);
            response.EnsureSuccessStatusCode();
        }
    }

    private static async Task<List<SimStatus>> WaitForSimsAsync(HttpClient client, int minimum, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var sims = await client.GetFromJsonAsync<List<SimStatus>>("/api/simulation/sims", ct);
            if (sims is not null && sims.Count >= minimum)
                return sims;

            await Task.Delay(50, ct);
        }

        throw new TimeoutException($"Expected at least {minimum} sims to appear in the snapshot");
    }

    private HubConnection CreateConnection(string playerId)
    {
        var server = factory.Server;
        var url = server.BaseAddress.ToString().TrimEnd('/') + "/hubs/world";

        var connection = new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(PlayerToken(playerId))!;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
            })
            .WithAutomaticReconnect()
            .Build();

        connections.Add(connection);
        return connection;
    }

    [Fact]
    public async Task Thirty_two_clients_receive_snapshots_and_submit_intents()
    {
        var ct = CancellationToken;
        var client = await StartSimulationAsync();
        await AddSimsAsync(client, PlayerCount, ct);
        await WaitForSimsAsync(client, PlayerCount, ct);

        var snapshots = new List<(string PlayerId, TaskCompletionSource<WorldSnapshot> Snapshot)>();
        var started = new List<HubConnection>();

        for (var i = 0; i < PlayerCount; i++)
        {
            var playerId = $"accept-player-{i}";
            var connection = CreateConnection(playerId);
            var tcs = new TaskCompletionSource<WorldSnapshot>();
            connection.On<WorldSnapshot>("SendSnapshot", snapshot => tcs.TrySetResult(snapshot));

            await connection.StartAsync(ct);
            started.Add(connection);
            snapshots.Add((playerId, tcs));
        }

        var received = await Task.WhenAll(snapshots.Select(entry =>
            entry.Snapshot.Task.WaitAsync(TimeSpan.FromSeconds(15), ct)));

        foreach (var snapshot in received)
        {
            Assert.NotNull(snapshot);
            Assert.True(snapshot.Sims.Count >= PlayerCount,
                $"Expected at least {PlayerCount} sims, got {snapshot.Sims.Count}");
            Assert.Equal(snapshot.Sims.Count, snapshot.Sims.Select(sim => sim.NetworkId).Distinct().Count());
        }

        var intentResults = await Task.WhenAll(started.Select((connection, index) =>
        {
            var payload = new
            {
                type = "move",
                playerId = $"accept-player-{index}",
                x = index * 2f,
                y = index * 3f
            };
            return connection.InvokeAsync<JsonElement>("SubmitIntent", payload, ct);
        }));

        foreach (var result in intentResults)
        {
            Assert.True(result.GetProperty("success").GetBoolean(),
                result.GetProperty("error").GetString());
        }
    }

    [Fact]
    public async Task Reconnect_inside_history_window_resumes_with_delta()
    {
        var ct = CancellationToken;
        var client = await StartSimulationAsync();
        await AddSimsAsync(client, 1, ct);
        var seeded = await WaitForSimsAsync(client, 1, ct);
        var expectedNetworkId = seeded[0].NetworkId;

        var playerId = "reconnect-player";
        var first = CreateConnection(playerId);
        var initial = new TaskCompletionSource<WorldSnapshot>();
        first.On<WorldSnapshot>("SendSnapshot", snapshot => initial.TrySetResult(snapshot));

        await first.StartAsync(ct);
        var snapshot = await initial.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        var acknowledgedTick = snapshot.Tick;
        await first.InvokeAsync("AcknowledgeSnapshot", acknowledgedTick, ct);
        await first.StopAsync(ct);

        await Task.Delay(300, ct);

        var second = CreateConnection(playerId);
        var reconnected = new TaskCompletionSource<WorldSnapshot>();
        second.On<WorldSnapshot>("SendSnapshot", value => reconnected.TrySetResult(value));
        var deltaTcs = new TaskCompletionSource<WorldSnapshotDelta>();
        second.On<WorldSnapshotDelta>("SendSnapshotDelta", delta => deltaTcs.TrySetResult(delta));

        await second.StartAsync(ct);
        await reconnected.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);

        await second.InvokeAsync("RequestSnapshot", (long?)acknowledgedTick, ct);
        var resumed = await deltaTcs.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);

        Assert.False(resumed.Full);
        Assert.Equal(acknowledgedTick, resumed.SinceTick);
        Assert.True(resumed.Tick >= acknowledgedTick);

        var resumedSim = Assert.Single(resumed.Sims, sim => sim.NetworkId == expectedNetworkId);
        Assert.True(resumedSim.X >= 0f);
        Assert.True(resumedSim.Y >= 0f);
    }

    [Fact]
    public async Task Reconnect_without_sinceTick_resumes_from_player_acknowledgement()
    {
        var ct = CancellationToken;
        var client = await StartSimulationAsync();
        await AddSimsAsync(client, 1, ct);
        await WaitForSimsAsync(client, 1, ct);

        var playerId = "auto-resume-player";
        var first = CreateConnection(playerId);
        var initial = new TaskCompletionSource<WorldSnapshot>();
        first.On<WorldSnapshot>("SendSnapshot", snapshot => initial.TrySetResult(snapshot));

        await first.StartAsync(ct);
        var snapshot = await initial.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        var acknowledgedTick = snapshot.Tick;
        await first.InvokeAsync("AcknowledgeSnapshot", acknowledgedTick, ct);
        await first.StopAsync(ct);

        await Task.Delay(300, ct);

        var second = CreateConnection(playerId);
        var reconnected = new TaskCompletionSource<WorldSnapshot>();
        second.On<WorldSnapshot>("SendSnapshot", value => reconnected.TrySetResult(value));
        var deltaTcs = new TaskCompletionSource<WorldSnapshotDelta>();
        second.On<WorldSnapshotDelta>("SendSnapshotDelta", delta => deltaTcs.TrySetResult(delta));

        await second.StartAsync(ct);
        await reconnected.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);

        await second.InvokeAsync("RequestSnapshot", (long?)null, ct);
        var resumed = await deltaTcs.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);

        Assert.False(resumed.Full);
        Assert.Equal(acknowledgedTick, resumed.SinceTick);
        Assert.True(resumed.Tick >= acknowledgedTick);
    }

    [Fact]
    public async Task Save_stop_restore_preserves_world()
    {
        var ct = CancellationToken;
        var client = await StartSimulationAsync();
        const int simCount = 5;
        await AddSimsAsync(client, simCount, ct);
        var before = await WaitForSimsAsync(client, simCount, ct);

        var save = await client.PostAsync("/api/simulation/save", null, ct);
        save.EnsureSuccessStatusCode();

        var stop = await client.PostAsync("/api/simulation/stop", null, ct);
        stop.EnsureSuccessStatusCode();

        var restore = await client.PostAsync("/api/simulation/restore", null, ct);
        restore.EnsureSuccessStatusCode();

        var after = await WaitForSimsAsync(client, before.Count, ct);

        Assert.True(after.Count >= before.Count,
            $"Expected at least {before.Count} sims after restore, got {after.Count}");
        Assert.All(after, sim => Assert.NotEqual(0UL, sim.NetworkId));

        var beforeById = before.ToDictionary(sim => sim.NetworkId);
        var afterById = after.ToDictionary(sim => sim.NetworkId);
        foreach (var (networkId, saved) in beforeById)
        {
            Assert.True(afterById.TryGetValue(networkId, out var restored),
                $"Network id {networkId} was not preserved across restore");
            Assert.Equal(saved.Name, restored.Name);
            Assert.Equal(saved.X, restored.X);
            Assert.Equal(saved.Y, restored.Y);
        }
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

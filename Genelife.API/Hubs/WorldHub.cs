using System.Collections.Concurrent;
using System.Text.Json;
using Genelife.Api.Auth;
using Genelife.Api.Commands;
using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Genelife.Api.Snapshots;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Genelife.Api.Hubs;

[Authorize]
public sealed class WorldHub : Hub
{
    private static readonly JsonSerializerOptions IntentJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IConnectionRegistry registry;
    private readonly SimulationEngine engine;
    private readonly ICommandQueue commandQueue;
    private readonly ReplicationTracker replicationTracker;
    private readonly ReplicationMetrics replicationMetrics;
    private readonly ViewportRegistry viewports;
    private readonly ILogger<WorldHub>? logger;
    private readonly int maxPayloadBytes;

    private static readonly ConcurrentDictionary<string, RateLimitWindow> RateLimits = new();
    private static readonly ConcurrentDictionary<string, RateLimitWindow> SnapshotRateLimits = new();
    private const int SnapshotRequestsPerSecond = 5;

    public WorldHub(
        IConnectionRegistry registry,
        SimulationEngine engine,
        ICommandQueue commandQueue,
        ReplicationTracker replicationTracker,
        ReplicationMetrics replicationMetrics,
        IConfiguration configuration,
        ViewportRegistry? viewports = null,
        ILogger<WorldHub>? logger = null)
    {
        this.registry = registry;
        this.engine = engine;
        this.commandQueue = commandQueue;
        this.replicationTracker = replicationTracker;
        this.replicationMetrics = replicationMetrics;
        this.viewports = viewports ?? new ViewportRegistry();
        this.logger = logger;
        maxPayloadBytes = configuration.GetValue("SignalR:MaxIntentPayloadBytes", 4096);
    }

    public override async Task OnConnectedAsync()
    {
        var playerId = Context.User?.GetPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            logger?.LogWarning("Connection {ConnectionId} rejected: no PlayerId claim", Context.ConnectionId);
            Context.Abort();
            return;
        }

        registry.Register(Context.ConnectionId, playerId);
        replicationTracker.RestoreForConnection(Context.ConnectionId, playerId);
        engine.EnsurePlayerSpawn(playerId);

        await Groups.AddToGroupAsync(Context.ConnectionId, $"player-{playerId}");
        await Groups.AddToGroupAsync(Context.ConnectionId, "all");

        var snapshot = engine.GetLatestSnapshot();
        var map = engine.BuildMapStatus();
        await Clients.Caller.SendAsync("SendMapSnapshot", map);
        if (snapshot is not null)
        {
            replicationMetrics.Record(Context.ConnectionId, full: true, EstimateBytes(snapshot));
            await Clients.Caller.SendAsync("SendSnapshot", snapshot);
        }

        logger?.LogInformation("Player {PlayerId} connected on {ConnectionId}", playerId, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (registry.TryUnregister(Context.ConnectionId, out var playerId) && playerId is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"player-{playerId}");
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "all");
        }

        RateLimits.TryRemove(Context.ConnectionId, out _);
        SnapshotRateLimits.TryRemove(Context.ConnectionId, out _);
        viewports.Remove(Context.ConnectionId);
        replicationTracker.Remove(Context.ConnectionId);
        replicationMetrics.Remove(Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Resends state for a reconnecting client. When <paramref name="sinceTick"/>
    /// is inside the retained history window the caller receives
    /// <c>SendSnapshotDelta</c>; otherwise a full <c>SendSnapshot</c> is sent.
    /// Omitting the argument resumes from the connection's last acknowledged tick.
    /// </summary>
    public async Task RequestSnapshot(long? sinceTick)
    {
        if (!CheckRateLimit(SnapshotRateLimits, Context.ConnectionId, SnapshotRequestsPerSecond, out var error))
        {
            await Clients.Caller.SendAsync("SnapshotRejected", error);
            return;
        }

        var effectiveSince = sinceTick ?? replicationTracker.GetAcknowledgedTick(Context.ConnectionId);
        var delta = engine.SnapshotHistory.BuildDelta(effectiveSince);
        if (delta is null)
            return;

        var viewport = viewports.TryGet(Context.ConnectionId, out var activeViewport) ? activeViewport : (Viewport?)null;
        var ownSim = engine.GetPlayerSimNetworkId(Context.User?.GetPlayerId() ?? string.Empty);

        if (delta.Full)
        {
            var full = new WorldSnapshot(delta.Tick, delta.Version, delta.Sims, delta.Companies);
            if (viewport is { } fullViewport)
                full = AoiFilter.FilterSnapshotByViewport(full, fullViewport, ownSim);
            replicationMetrics.Record(Context.ConnectionId, full: true, EstimateBytes(full));
            await Clients.Caller.SendAsync("SendSnapshot", full);
            return;
        }

        if (viewport is { } active)
            delta = AoiFilter.FilterDeltaByViewport(delta, active, ownSim);

        replicationMetrics.Record(Context.ConnectionId, full: false, EstimateBytes(delta));
        await Clients.Caller.SendAsync("SendSnapshotDelta", delta);
    }

    public async Task SendMapSnapshot()
    {
        await Clients.Caller.SendAsync("SendMapSnapshot", engine.BuildMapStatus());
    }

    public Task RequestMapSnapshot() => SendMapSnapshot();

    public bool SetViewport(int centerX, int centerY, int radius)
    {
        var map = engine.MapContext;
        if (map is null)
            return false;
        radius = Math.Clamp(radius, 1, Math.Max(map.Options.Width, map.Options.Height));
        var center = map.Options.Bounds.Clamp(new Genelife.Map.TileCoord(centerX, centerY));
        return viewports.TrySet(Context.ConnectionId, new Viewport(center.X, center.Y, radius));
    }

    public void ClearViewport() => viewports.Remove(Context.ConnectionId);

    /// <summary>
    /// Records the last world tick the client has applied. Subsequent broadcast
    /// updates are sent as deltas from this tick while it remains in history.
    /// </summary>
    public void AcknowledgeSnapshot(long tick)
    {
        var latest = engine.SnapshotHistory.LatestTick;
        if (tick < 0 || latest < 0 || tick > latest)
            return;

        var playerId = Context.User?.GetPlayerId();
        replicationTracker.Acknowledge(Context.ConnectionId, playerId ?? string.Empty, tick);
    }

    public async Task<IntentResult> SubmitIntent(JsonElement payload)
    {
        if (!CheckRateLimit(Context.ConnectionId, out var error))
        {
            return new IntentResult(false, error);
        }

        if (payload.ValueKind != JsonValueKind.Object)
        {
            return new IntentResult(false, "Payload must be a JSON object");
        }

        var rawLength = payload.GetRawText().Length;
        if (rawLength > maxPayloadBytes)
        {
            return new IntentResult(false, $"Payload exceeds {maxPayloadBytes} byte limit");
        }

        if (!registry.TryGetPlayerId(Context.ConnectionId, out var playerId) || playerId is null)
        {
            return new IntentResult(false, "Connection not registered");
        }

        Intent intent;
        try
        {
            intent = payload.Deserialize<Intent>(IntentJsonOptions)
                ?? throw new InvalidOperationException("Deserialization returned null");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            return new IntentResult(false, $"Malformed payload: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(intent.PlayerId))
        {
            return new IntentResult(false, "Intent missing PlayerId");
        }

        if (!string.Equals(intent.PlayerId, playerId, StringComparison.Ordinal))
        {
            return new IntentResult(false, "PlayerId mismatch");
        }

        if (!commandQueue.TryEnqueue(intent, out var queueError))
        {
            return new IntentResult(false, queueError ?? "Failed to enqueue intent");
        }

        return new IntentResult(true, null);
    }

    private static int EstimateBytes(object payload) => ReplicationMetrics.EstimateBytes(payload);

    private bool CheckRateLimit(string connectionId, out string? error) =>
        CheckRateLimit(RateLimits, connectionId, maxPerWindow: 100, out error);

    private static bool CheckRateLimit(
        ConcurrentDictionary<string, RateLimitWindow> limits,
        string connectionId,
        int maxPerWindow,
        out string? error)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        const long windowMs = 1000;

        var window = limits.GetOrAdd(connectionId, _ => new RateLimitWindow(now));

        lock (window)
        {
            if (now - window.Start >= windowMs)
            {
                window.Start = now;
                window.Count = 0;
            }

            if (window.Count >= maxPerWindow)
            {
                error = $"Rate limit exceeded ({maxPerWindow} requests per second)";
                return false;
            }

            window.Count++;
            error = null;
            return true;
        }
    }

    private sealed class RateLimitWindow
    {
        public long Start;
        public int Count;

        public RateLimitWindow(long start)
        {
            Start = start;
        }
    }
}

public sealed record IntentResult(bool Success, string? Error);
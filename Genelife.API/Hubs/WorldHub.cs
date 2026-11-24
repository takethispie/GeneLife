using System.Collections.Concurrent;
using System.Text.Json;
using Genelife.Api.Auth;
using Genelife.Api.Commands;
using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Genelife.Api.Snapshots;
using Genelife.Social;
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
    private readonly ChatService chat;
    private readonly EmoteService emotes;
    private readonly ILogger<WorldHub>? logger;
    private readonly int maxPayloadBytes;
    private readonly int defaultViewportRadius;
    private readonly int minViewportRadius;
    private readonly int maxViewportRadius;
    private readonly int tickIntervalMs;
    private readonly bool autoCreatePlayerSim;

    private static readonly ConcurrentDictionary<string, RateLimitWindow> RateLimits = new();
    private static readonly ConcurrentDictionary<string, RateLimitWindow> SnapshotRateLimits = new();
    private static readonly ConcurrentDictionary<string, RateLimitWindow> ViewportRateLimits = new();
    private const int SnapshotRequestsPerSecond = 5;
    private const int ViewportRequestsPerSecond = 10;

    public WorldHub(
        IConnectionRegistry registry,
        SimulationEngine engine,
        ICommandQueue commandQueue,
        ReplicationTracker replicationTracker,
        ReplicationMetrics replicationMetrics,
        IConfiguration configuration,
        ChatService chat,
        EmoteService emotes,
        ViewportRegistry? viewports = null,
        ILogger<WorldHub>? logger = null)
    {
        this.registry = registry;
        this.engine = engine;
        this.commandQueue = commandQueue;
        this.replicationTracker = replicationTracker;
        this.replicationMetrics = replicationMetrics;
        this.viewports = viewports ?? new ViewportRegistry();
        this.chat = chat;
        this.emotes = emotes;
        this.logger = logger;
        maxPayloadBytes = configuration.GetValue("SignalR:MaxIntentPayloadBytes", 4096);
        defaultViewportRadius = Math.Max(1, configuration.GetValue("SignalR:Viewport:DefaultRadius", 32));
        minViewportRadius = Math.Max(1, configuration.GetValue("SignalR:Viewport:MinRadius", 4));
        maxViewportRadius = Math.Max(minViewportRadius, configuration.GetValue("SignalR:Viewport:MaxRadius", 256));
        tickIntervalMs = Math.Max(1, configuration.GetValue("SignalR:TickIntervalMs", 40));
        autoCreatePlayerSim = configuration.GetValue("SignalR:AutoCreatePlayerSim", true);
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
        engine.MarkPlayerConnected(playerId);
        if (autoCreatePlayerSim)
            engine.EnsurePlayerSim(playerId);
        engine.EnsurePlayerSpawn(playerId);

        if (viewports.TryGetForPlayer(playerId, out var savedViewport))
        {
            viewports.TrySet(Context.ConnectionId, savedViewport);
        }
        else if (DefaultViewportFor(playerId) is { } defaultViewport)
        {
            viewports.TrySet(Context.ConnectionId, defaultViewport);
            viewports.SetForPlayer(playerId, defaultViewport);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"player-{playerId}");
        await Groups.AddToGroupAsync(Context.ConnectionId, "all");

        var snapshot = engine.GetLatestSnapshot();
        var map = engine.BuildMapSnapshot();
        var simNetworkId = engine.GetPlayerSimNetworkId(playerId);

        await Clients.Caller.SendAsync("Welcome", new WelcomeMessage
        {
            PlayerId = playerId,
            SimNetworkId = simNetworkId,
            MapId = map.MapId,
            MapVersion = map.Version,
            Tick = snapshot?.Tick ?? engine.CurrentTick,
            TickIntervalMs = tickIntervalMs,
            SnapshotVersion = snapshot?.Version ?? WorldSnapshot.CurrentVersion,
            ProtocolVersion = WelcomeMessage.CurrentProtocolVersion
        });

        await Clients.Caller.SendAsync("SendMapSnapshot", map);
        if (snapshot is not null)
        {
            if (viewports.TryGet(Context.ConnectionId, out var initialViewport))
            {
                snapshot = AoiFilter.FilterSnapshotByViewport(
                    snapshot,
                    initialViewport,
                    simNetworkId);
            }

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
        ViewportRateLimits.TryRemove(Context.ConnectionId, out _);
        viewports.Remove(Context.ConnectionId);
        if (playerId is not null && registry.GetConnections(playerId).Count == 0)
        {
            viewports.RemovePlayer(playerId);
            engine.MarkPlayerDisconnected(playerId);
        }

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

        var forceFull = replicationTracker.NeedsFullSnapshot(Context.ConnectionId);
        var effectiveSince = forceFull
            ? 0
            : sinceTick ?? replicationTracker.GetAcknowledgedTick(Context.ConnectionId);
        var viewport = viewports.TryGet(Context.ConnectionId, out var activeViewport) ? activeViewport : (Viewport?)null;
        var ownSim = engine.GetPlayerSimNetworkId(Context.User?.GetPlayerId() ?? string.Empty);

        WorldSnapshotDelta? delta;
        if (forceFull)
        {
            var current = engine.GetLatestSnapshot();
            if (current is null)
                return;

            var full = viewport is { } forcedViewport
                ? AoiFilter.FilterSnapshotByViewport(current, forcedViewport, ownSim)
                : current;
            delta = new WorldSnapshotDelta
            {
                Tick = full.Tick,
                SinceTick = 0,
                Version = full.Version,
                Full = true,
                Sims = full.Sims,
                Companies = full.Companies
            };
        }
        else if (viewport is { } active)
        {
            var current = engine.GetLatestSnapshot();
            if (current is null)
                return;

            var previous = effectiveSince > 0
                && engine.SnapshotHistory.TryGet(effectiveSince, out var found)
                    ? found
                    : null;
            delta = AoiFilter.BuildViewportDelta(previous, current, active, ownSim);
        }
        else
        {
            delta = engine.SnapshotHistory.BuildDelta(effectiveSince);
        }

        if (delta is null)
            return;

        if (delta.Full)
        {
            var full = new WorldSnapshot(delta.Tick, delta.Version, delta.Sims, delta.Companies);
            replicationMetrics.Record(Context.ConnectionId, full: true, EstimateBytes(full));
            await Clients.Caller.SendAsync("SendSnapshot", full);
            replicationTracker.MarkFullSnapshotSent(Context.ConnectionId, full.Tick);
            return;
        }

        replicationMetrics.Record(Context.ConnectionId, full: false, EstimateBytes(delta));
        await Clients.Caller.SendAsync("SendSnapshotDelta", delta);
    }

    public async Task SendMapSnapshot()
    {
        if (!CheckRateLimit(SnapshotRateLimits, Context.ConnectionId, SnapshotRequestsPerSecond, out var error))
        {
            await Clients.Caller.SendAsync("SnapshotRejected", error);
            return;
        }

        await Clients.Caller.SendAsync("SendMapSnapshot", engine.BuildMapSnapshot());
    }

    public Task RequestMapSnapshot() => SendMapSnapshot();

    public ViewportResult SetViewport(SetViewportRequest request)
    {
        if (engine.MapContext is null)
            return new ViewportResult(false, request.CenterX, request.CenterY, 0, "Map is not available");

        if (!CheckRateLimit(ViewportRateLimits, Context.ConnectionId, ViewportRequestsPerSecond, out var error))
            return new ViewportResult(false, request.CenterX, request.CenterY, 0, error);

        var radius = ClampViewportRadius(request.Radius);
        var center = ClampViewportCenter(request.CenterX, request.CenterY);
        var viewport = new Viewport(center.X, center.Y, radius);

        viewports.TrySet(Context.ConnectionId, viewport);
        var playerId = Context.User?.GetPlayerId();
        if (!string.IsNullOrWhiteSpace(playerId))
            viewports.SetForPlayer(playerId, viewport);

        replicationTracker.Invalidate(Context.ConnectionId);
        return new ViewportResult(true, center.X, center.Y, radius, null);
    }

    public void ClearViewport()
    {
        var playerId = Context.User?.GetPlayerId();
        viewports.Remove(Context.ConnectionId);
        if (!string.IsNullOrWhiteSpace(playerId))
            viewports.RemovePlayer(playerId);

        replicationTracker.Invalidate(Context.ConnectionId);
    }

    private Viewport? DefaultViewportFor(string playerId)
    {
        if (engine.GetPlayerTile(playerId) is not { } tile)
            return null;

        var center = ClampViewportCenter(tile.X, tile.Y);
        return new Viewport(center.X, center.Y, ClampViewportRadius(defaultViewportRadius));
    }

    private int ClampViewportRadius(int radius)
    {
        var map = engine.MapContext;
        var max = maxViewportRadius;
        if (map is not null)
            max = Math.Min(max, Math.Max(map.Options.Width, map.Options.Height));

        max = Math.Max(minViewportRadius, max);
        return Math.Clamp(radius, minViewportRadius, max);
    }

    private Genelife.Map.TileCoord ClampViewportCenter(int x, int y)
    {
        var map = engine.MapContext;
        return map is null
            ? new Genelife.Map.TileCoord(x, y)
            : map.Options.Bounds.Clamp(new Genelife.Map.TileCoord(x, y));
    }

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

        var reservedPlayerCommand = false;
        if (intent is PlayerActionIntent)
        {
            if (!engine.TryReservePlayerCommand(playerId))
                return new IntentResult(false, "Too many player actions are already pending");
            reservedPlayerCommand = true;
        }

        if (!commandQueue.TryEnqueue(intent, out var queueError))
        {
            if (reservedPlayerCommand)
                engine.ReleasePlayerCommand(playerId);
            return new IntentResult(false, queueError ?? "Failed to enqueue intent");
        }

        return new IntentResult(true, null);
    }

    /// <summary>
    /// Admits a chat line and pushes it to the resolved recipients. Chat is
    /// transport-only: it never enters a snapshot, a delta or a tick system.
    /// </summary>
    public async Task<ChatSendResult> SendChat(ChatSendRequest request)
    {
        if (!registry.TryGetPlayerId(Context.ConnectionId, out var playerId) || playerId is null)
            return ChatSendResult.Reject("not_registered", "Connection not registered");

        var result = await chat.SendAsync(
            playerId,
            request,
            engine.CurrentTick,
            Context.ConnectionAborted);

        if (result.Accepted && result.Message is not null)
        {
            foreach (var recipient in result.Recipients)
            {
                await Clients.Group($"player-{recipient}")
                    .SendAsync("ReceiveChat", result.Message, Context.ConnectionAborted);
            }
        }

        return result;
    }

    /// <summary>Admits a predefined emote and broadcasts it to the public audience.</summary>
    public async Task<EmoteSendResult> SendEmote(EmoteSendRequest request)
    {
        if (!registry.TryGetPlayerId(Context.ConnectionId, out var playerId) || playerId is null)
            return EmoteSendResult.Reject("not_registered", "Connection not registered");

        var result = emotes.Send(playerId, request.Emote, engine.CurrentTick);
        if (result.Accepted && result.Emote is not null)
        {
            foreach (var recipient in result.Recipients)
            {
                await Clients.Group($"player-{recipient}")
                    .SendAsync("ReceiveEmote", result.Emote, Context.ConnectionAborted);
            }
        }

        return result;
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
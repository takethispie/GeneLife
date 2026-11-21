using System.Collections.Concurrent;
using System.Text.Json;
using Genelife.Api.Auth;
using Genelife.Api.Commands;
using Genelife.Api.Services;
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
    private readonly ILogger<WorldHub>? logger;
    private readonly int maxPayloadBytes;

    private static readonly ConcurrentDictionary<string, RateLimitWindow> RateLimits = new();

    public WorldHub(
        IConnectionRegistry registry,
        SimulationEngine engine,
        ICommandQueue commandQueue,
        IConfiguration configuration,
        ILogger<WorldHub>? logger = null)
    {
        this.registry = registry;
        this.engine = engine;
        this.commandQueue = commandQueue;
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

        await Groups.AddToGroupAsync(Context.ConnectionId, $"player-{playerId}");
        await Groups.AddToGroupAsync(Context.ConnectionId, "all");

        var snapshot = engine.GetLatestSnapshot();
        if (snapshot is not null)
        {
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

        await base.OnDisconnectedAsync(exception);
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

    private bool CheckRateLimit(string connectionId, out string? error)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        const long windowMs = 1000;
        const int maxPerWindow = 100;

        var window = RateLimits.GetOrAdd(connectionId, _ => new RateLimitWindow(now));

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
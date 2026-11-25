using System.Text.Json;
using Genelife.Api.Commands;

namespace Genelife.Api.Services;

public readonly record struct PlayerCommandAdmission(
    bool Accepted,
    Guid CommandId,
    string? Code,
    string? Message);

/// <summary>
/// HTTP/hub admission for player commands. It enforces per-player rate and
/// queue budgets and enqueues a typed command; it never mutates the world.
/// Ownership, proximity and domain validation happen on the tick thread.
/// </summary>
public sealed class PlayerCommandService
{
    private const int MaxPayloadBytes = 4096;
    private const int MaxCommandsPerSecond = 10;

    private readonly ICommandQueue? queue;
    private readonly SimulationEngine engine;
    private readonly RequestRateLimiter limiter;

    public PlayerCommandService(
        SimulationEngine engine,
        RequestRateLimiter limiter,
        ICommandQueue? queue = null)
    {
        this.engine = engine;
        this.limiter = limiter;
        this.queue = queue;
    }

    public PlayerCommandAdmission Admit(
        string? playerId,
        PlayerActionKind kind,
        ulong genosNetworkId,
        JsonElement payload,
        long sequence,
        Guid? commandId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            return Reject("unauthenticated", "A player identity is required.");
        if (queue is null)
            return Reject("world_not_ready", "The command queue is not available.");
        if (!engine.IsRunning)
            return Reject("world_not_ready", "The world is not ready yet.");
        if (payload.ValueKind != JsonValueKind.Object)
            return Reject("malformed_payload", "A payload object is required.");

        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        if (payloadBytes.Length > MaxPayloadBytes)
            return Reject("payload_too_large", "The payload is too large.");

        if (!limiter.TryAcquire($"player-command:{playerId}", MaxCommandsPerSecond, TimeSpan.FromSeconds(1)))
            return Reject("rate_limited", "Too many actions. Try again shortly.");

        if (!engine.TryReservePlayerCommand(playerId))
            return Reject("rate_limited", "Too many actions are already pending.");

        var id = commandId is null || commandId == Guid.Empty ? Guid.NewGuid() : commandId.Value;
        var resolvedGenos = genosNetworkId != 0 ? genosNetworkId : engine.GetPlayerGenosNetworkId(playerId);
        var intent = new PlayerActionIntent
        {
            PlayerId = playerId,
            Sequence = sequence,
            GenosNetworkId = resolvedGenos,
            Kind = kind,
            Payload = payload,
            CommandId = id,
            SubmittedTick = engine.CurrentTick
        };

        if (!queue.TryEnqueue(intent, out var error))
        {
            engine.ReleasePlayerCommand(playerId);
            return Reject("queue_full", error ?? "The command queue is full.");
        }

        engine.MarkPlayerConnected(playerId);
        return new PlayerCommandAdmission(true, id, null, null);
    }

    private static PlayerCommandAdmission Reject(string code, string message) =>
        new(false, Guid.Empty, code, message);
}

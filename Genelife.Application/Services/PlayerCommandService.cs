using System.Text.Json;
using Genelife.Api.Commands;

namespace Genelife.Api.Services;

public readonly record struct PlayerCommandAdmission(
    bool Accepted,
    Guid CommandId,
    string? Code,
    string? Message)
{
    /// <summary>Refuses with a member of the closed player-action refusal set.</summary>
    public static PlayerCommandAdmission Reject(PlayerRefusal refusal, string message) =>
        new(false, Guid.Empty, refusal.WireCode(), message);

    /// <summary>
    /// Refuses with a code the closed set does not name. Kept for the rest of the Application layer.
    /// </summary>
    [Obsolete("Prefer Reject(PlayerRefusal, string). Kept for the rest of the Application layer while it still produces raw codes.")]
    public static PlayerCommandAdmission Reject(string code, string message) =>
        new(false, Guid.Empty, code, message);
}

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
            return PlayerCommandAdmission.Reject(PlayerRefusal.Unauthenticated, "A player identity is required.");
        if (queue is null)
            return PlayerCommandAdmission.Reject(PlayerRefusal.WorldNotReady, "The command queue is not available.");
        if (!engine.IsRunning)
            return PlayerCommandAdmission.Reject(PlayerRefusal.WorldNotReady, "The world is not ready yet.");
        if (payload.ValueKind != JsonValueKind.Object)
            return PlayerCommandAdmission.Reject(PlayerRefusal.MalformedPayload, "A payload object is required.");

        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        if (payloadBytes.Length > MaxPayloadBytes)
            return PlayerCommandAdmission.Reject(PlayerRefusal.PayloadTooLarge, "The payload is too large.");

        if (!limiter.TryAcquire($"player-command:{playerId}", MaxCommandsPerSecond, TimeSpan.FromSeconds(1)))
            return PlayerCommandAdmission.Reject(PlayerRefusal.RateLimited, "Too many actions. Try again shortly.");

        if (!engine.TryReservePlayerCommand(playerId))
            return PlayerCommandAdmission.Reject(PlayerRefusal.RateLimited, "Too many actions are already pending.");

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
            return PlayerCommandAdmission.Reject(PlayerRefusal.QueueFull, error ?? "The command queue is full.");
        }

        engine.MarkPlayerConnected(playerId);
        return new PlayerCommandAdmission(true, id, null, null);
    }
}

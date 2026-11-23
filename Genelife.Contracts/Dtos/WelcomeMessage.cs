namespace Genelife.Api.DTOs;

/// <summary>
/// Sent to a client immediately after the hub connection is admitted, before
/// the initial map and world snapshots. It gives the client the identity of its
/// own sim and the timing/protocol information it needs to interpolate without
/// guessing which <see cref="SimStatus.IsPlayerControlled"/> sim belongs to the
/// player.
/// </summary>
public sealed class WelcomeMessage
{
    /// <summary>Current contract protocol version understood by the server.</summary>
    public const int CurrentProtocolVersion = 1;

    public string PlayerId { get; set; } = string.Empty;
    public ulong SimNetworkId { get; set; }
    public string MapId { get; set; } = string.Empty;
    public int MapVersion { get; set; }
    public long Tick { get; set; }
    public int TickIntervalMs { get; set; } = 40;
    public int SnapshotVersion { get; set; }
    public int ProtocolVersion { get; set; } = CurrentProtocolVersion;
}

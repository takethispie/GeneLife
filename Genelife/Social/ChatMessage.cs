namespace Genelife.Social;

public enum ChatChannel : byte { Say, Whisper, Venue, System }

public enum EmoteKind : byte { Wave, Nod, ShakeHead, Sit, Stand, Cheer, Point }

/// <summary>
/// One player-authored or server-authored chat line. Chat is not simulation
/// state: it is never stored in the world snapshot and never read from a tick
/// system.
/// </summary>
public sealed record ChatMessage
{
    public Guid Id { get; init; }
    public string PlayerId { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public ChatChannel Channel { get; init; }
    public string Text { get; init; } = string.Empty;
    public long Tick { get; init; }
    public string? TargetPlayerId { get; init; }
    public ulong? VenueNetworkId { get; init; }
}

/// <summary>
/// A predefined, payload-free social gesture. Emotes are ephemeral and are
/// never persisted.
/// </summary>
public sealed record ChatEmote
{
    public Guid Id { get; init; }
    public string PlayerId { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public EmoteKind Emote { get; init; }
    public long Tick { get; init; }
    public ulong? VenueNetworkId { get; init; }
}

namespace Genelife.Social;

public enum SocialEdgeKind : byte { Friend, Block, Mute }

public enum SocialEdgeSource : byte { Request, Admin, Migration, Player }

/// <summary>
/// A social relationship keyed by player id, not genos entity. Friends are
/// mirrored; blocks are enforced symmetrically; mutes are one-directional.
/// </summary>
public sealed record SocialEdge
{
    public required string FromPlayerId { get; init; }
    public required string ToPlayerId { get; init; }
    public SocialEdgeKind Kind { get; init; }
    public long SinceTick { get; init; }
    public SocialEdgeSource Source { get; init; }
}

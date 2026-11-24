namespace Genelife.Social;

public enum SocialRequestKind : byte { Friend }

public enum SocialRequestStatus : byte { Pending, Accepted, Declined, Expired, Cancelled }

public sealed record SocialRequest
{
    public Guid Id { get; init; }
    public string FromPlayerId { get; init; } = string.Empty;
    public string ToPlayerId { get; init; } = string.Empty;
    public SocialRequestKind Kind { get; init; }
    public SocialRequestStatus Status { get; init; }
    public long CreatedTick { get; init; }
    public long ExpiresTick { get; init; }
}

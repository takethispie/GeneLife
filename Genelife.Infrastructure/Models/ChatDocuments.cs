namespace Genelife.Api.Models;

public sealed class ChatMessageDocument
{
    public Guid Id { get; set; }
    public string PlayerId { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public byte Channel { get; set; }
    public string Text { get; set; } = string.Empty;
    public long Tick { get; set; }
    public string? TargetPlayerId { get; set; }
    public ulong? VenueNetworkId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

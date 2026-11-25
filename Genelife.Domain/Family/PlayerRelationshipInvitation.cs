using Genelife.Components.Family;

namespace Genelife.Family;

/// <summary>
/// A player-keyed consent request. Unlike <see cref="FamilyInvitation"/>, this
/// record survives heir continuation: acceptance resolves each player's
/// current controlled genos and revalidates before creating any relationship.
/// </summary>
public sealed record PlayerRelationshipInvitation
{
    public Guid Id { get; init; }
    public string FromPlayerId { get; init; } = string.Empty;
    public string ToPlayerId { get; init; } = string.Empty;
    public FamilyInvitationKind Kind { get; init; }
    public FamilyInvitationStatus Status { get; init; }
    public long CreatedTick { get; init; }
    public long ExpiresTick { get; init; }
}

public sealed record PlayerRelationshipStatus(
    string PlayerId,
    string? PartnerPlayerId,
    ulong? PartnerGenosNetworkId,
    string? PartnerName,
    bool SharesHousehold,
    bool CanCohabit,
    IReadOnlyList<PlayerRelationshipInvitation> Invitations);

public readonly record struct PlayerRelationshipResult(
    bool Applied,
    string? Code,
    string? Message,
    PlayerRelationshipInvitation? Invitation = null,
    object? Data = null)
{
    public static PlayerRelationshipResult Success(
        string? message = null,
        PlayerRelationshipInvitation? invitation = null,
        object? data = null) =>
        new(true, null, message, invitation, data);

    public static PlayerRelationshipResult Reject(string code, string message) =>
        new(false, code, message);
}

public sealed class PlayerRelationshipOptions
{
    public int InvitationLifetimeTicks { get; set; } = 100;
    public int DeclineCooldownTicks { get; set; } = 600;

    public void Validate()
    {
        if (InvitationLifetimeTicks < 1)
            throw new InvalidOperationException("InvitationLifetimeTicks must be positive.");
        if (DeclineCooldownTicks < 0)
            throw new InvalidOperationException("DeclineCooldownTicks cannot be negative.");
    }
}

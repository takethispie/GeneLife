namespace Genelife.Api.DTOs;

public sealed record PlayerRelationshipInvitationStatus(
    Guid Id,
    string FromPlayerId,
    string ToPlayerId,
    string Kind,
    string Status,
    long ExpiresTick,
    long CreatedTick);

public sealed record PlayerRelationshipStatusDto(
    string PlayerId,
    string? PartnerPlayerId,
    ulong? PartnerGenosNetworkId,
    string? PartnerName,
    bool SharesHousehold,
    bool CanCohabit,
    IReadOnlyList<PlayerRelationshipInvitationStatus> Invitations);

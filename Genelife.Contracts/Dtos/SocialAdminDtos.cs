using Genelife.Api.Social;

namespace Genelife.Api.DTOs;

public sealed record SocialVenueAdminDto(
    ulong VenueNetworkId,
    string Name,
    string Kind,
    int Occupancy,
    int Capacity,
    bool IsOpen,
    bool IsClosed);

public sealed record SocialReportAdminDto(
    Guid Id,
    string ReporterPlayerId,
    string TargetPlayerId,
    string Reason,
    Guid? MessageId,
    string? Evidence,
    long CreatedTick,
    string Status);

public sealed record SocialEdgeAdminDto(
    string FromPlayerId,
    string ToPlayerId,
    string Kind,
    long SinceTick,
    string Source);

public sealed record SocialRequestAdminDto(
    Guid Id,
    string FromPlayerId,
    string ToPlayerId,
    string Kind,
    string Status,
    long CreatedTick,
    long ExpiresTick);

public sealed record PlayerRelationshipInvitationAdminDto(
    Guid Id,
    string FromPlayerId,
    string ToPlayerId,
    string Kind,
    string Status,
    long CreatedTick,
    long ExpiresTick);

public sealed record ModerationActionAdminDto(
    Guid Id,
    Guid? ReportId,
    Guid? ReversesActionId,
    string ModeratorId,
    string TargetPlayerId,
    string? CounterpartyPlayerId,
    string Kind,
    long DurationTicks,
    long CreatedTick,
    long? EndsTick,
    string Reason,
    string? Evidence,
    Guid? MessageId,
    ulong? VenueNetworkId);

public sealed record SocialPrivacyAdminDto(
    string PlayerId,
    bool ShowDisplayName,
    bool AllowNearbyForNonFriends,
    bool FriendsOnlyMessages);

public sealed record ChatRetentionAdminDto(
    int RetainedMessages,
    int DeletedMessages,
    DateTimeOffset? OldestRetained,
    DateTimeOffset CheckedAt,
    int RetentionDays);

public sealed record SocialAdminOverviewDto(
    long Tick,
    IReadOnlyList<PlayerPresenceDto> Online,
    IReadOnlyList<SocialVenueAdminDto> Venues,
    IReadOnlyList<SocialReportAdminDto> Reports,
    IReadOnlyList<SocialEdgeAdminDto> Edges,
    IReadOnlyList<SocialRequestAdminDto> Requests,
    IReadOnlyList<PlayerRelationshipInvitationAdminDto> PartnershipInvitations,
    IReadOnlyList<ModerationActionAdminDto> ModerationActions,
    IReadOnlyList<SocialPrivacyAdminDto> Privacy,
    ChatRetentionAdminDto ChatRetention);

using Genelife.Api.DTOs;

namespace Genelife.Api.Social;

public sealed record PlayerPresenceDto(
    string PlayerId,
    string? DisplayName,
    ulong SimNetworkId,
    int TileX,
    int TileY,
    ulong? BuildingNetworkId,
    string State,
    AppearanceDto? Appearance,
    long LastSeenTick);

public sealed record PlayerDirectoryDto(
    string Scope,
    int Radius,
    IReadOnlyList<PlayerPresenceDto> Items);

public sealed record SocialPrivacyDto(bool ShowDisplayName, bool AllowNearbyForNonFriends);

public sealed record SocialPrivacyUpdateRequest(bool ShowDisplayName, bool AllowNearbyForNonFriends);

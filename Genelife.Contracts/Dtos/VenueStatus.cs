namespace Genelife.Api.DTOs;

public sealed record VenueOccupantDto(
    string PlayerId,
    string? DisplayName,
    ulong SimNetworkId);

public sealed record VenueActivityNoticeDto(
    string PlayerId,
    string? DisplayName,
    string Activity,
    long Tick);

public sealed record VenueStatusDto(
    ulong VenueNetworkId,
    string Name,
    string Kind,
    int Occupancy,
    int Capacity,
    bool IsOpen,
    bool IsClosed,
    string Activities,
    IReadOnlyList<VenueOccupantDto> Occupants,
    IReadOnlyList<VenueActivityNoticeDto> RecentActivities);

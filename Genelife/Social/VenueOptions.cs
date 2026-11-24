using Genelife.Components.Social;

namespace Genelife.Social;

public sealed class VenueOptions
{
    public int TicksPerDay { get; set; } = 24;
    public int CafeCapacity { get; set; } = 8;
    public int ParkCapacity { get; set; } = 20;
    public int CivicCapacity { get; set; } = 16;
    public int OfficeLobbyCapacity { get; set; } = 12;
    public int SchoolYardCapacity { get; set; } = 24;

    public int CafeOpenFromTick { get; set; } = 7;
    public int CafeOpenToTick { get; set; } = 22;
    public int ParkOpenFromTick { get; set; } = 6;
    public int ParkOpenToTick { get; set; } = 22;
    public int CivicOpenFromTick { get; set; } = 6;
    public int CivicOpenToTick { get; set; } = 22;
    public int OfficeLobbyOpenFromTick { get; set; } = 8;
    public int OfficeLobbyOpenToTick { get; set; } = 18;
    public int SchoolYardOpenFromTick { get; set; } = 8;
    public int SchoolYardOpenToTick { get; set; } = 15;

    public int ActivityCooldownTicks { get; set; } = 5;
    public int RejectionNoticeCooldownTicks { get; set; } = 25;
    public int RecentActivityTicks { get; set; } = 100;
    public int RecentActivityLimit { get; set; } = 8;
    public int ConfigureRefreshTicks { get; set; } = 25;

    public void Validate()
    {
        if (TicksPerDay < 1)
            throw new InvalidOperationException("Venue:TicksPerDay must be positive.");
        if (CafeCapacity < 1 || ParkCapacity < 1 || CivicCapacity < 1 ||
            OfficeLobbyCapacity < 1 || SchoolYardCapacity < 1)
            throw new InvalidOperationException("Venue capacities must be positive.");
        if (ActivityCooldownTicks < 0)
            throw new InvalidOperationException("Venue activity cooldown cannot be negative.");
        if (RecentActivityTicks < 1 || RecentActivityLimit < 1)
            throw new InvalidOperationException("Venue recent activity settings must be positive.");
        if (ConfigureRefreshTicks < 1)
            throw new InvalidOperationException("Venue configure refresh interval must be positive.");
    }

    public SocialVenue DefaultsFor(SocialVenueKind kind) => kind switch
    {
        SocialVenueKind.Cafe => new SocialVenue
        {
            Kind = kind,
            Capacity = CafeCapacity,
            OpenFromTick = CafeOpenFromTick,
            OpenToTick = CafeOpenToTick,
            Activities = VenueActivityMask.Sit | VenueActivityMask.Talk
                | VenueActivityMask.ShareMeal | VenueActivityMask.Cheer
        },
        SocialVenueKind.Park => new SocialVenue
        {
            Kind = kind,
            Capacity = ParkCapacity,
            OpenFromTick = ParkOpenFromTick,
            OpenToTick = ParkOpenToTick,
            Activities = VenueActivityMask.Sit | VenueActivityMask.Talk
                | VenueActivityMask.PlayGame | VenueActivityMask.Cheer
        },
        SocialVenueKind.Civic => new SocialVenue
        {
            Kind = kind,
            Capacity = CivicCapacity,
            OpenFromTick = CivicOpenFromTick,
            OpenToTick = CivicOpenToTick,
            Activities = VenueActivityMask.Sit | VenueActivityMask.Talk
                | VenueActivityMask.ShareMeal | VenueActivityMask.Cheer
        },
        SocialVenueKind.OfficeLobby => new SocialVenue
        {
            Kind = kind,
            Capacity = OfficeLobbyCapacity,
            OpenFromTick = OfficeLobbyOpenFromTick,
            OpenToTick = OfficeLobbyOpenToTick,
            Activities = VenueActivityMask.Sit | VenueActivityMask.Talk | VenueActivityMask.Cheer
        },
        _ => new SocialVenue
        {
            Kind = SocialVenueKind.SchoolYard,
            Capacity = SchoolYardCapacity,
            OpenFromTick = SchoolYardOpenFromTick,
            OpenToTick = SchoolYardOpenToTick,
            Activities = VenueActivityMask.Sit | VenueActivityMask.Talk
                | VenueActivityMask.PlayGame | VenueActivityMask.Cheer
        }
    };
}

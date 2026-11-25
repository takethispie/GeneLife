namespace Genelife.Components.Social;

public enum SocialVenueKind : byte { Cafe, Park, Civic, OfficeLobby, SchoolYard }

public enum VenueActivity : byte { Sit, Talk, ShareMeal, PlayGame, Cheer }

[Flags]
public enum VenueActivityMask : byte
{
    None = 0,
    Sit = 1 << 0,
    Talk = 1 << 1,
    ShareMeal = 1 << 2,
    PlayGame = 1 << 3,
    Cheer = 1 << 4
}

/// <summary>
/// Marks an existing building as a social venue. Opening ticks are interpreted
/// as a tick-of-day within <c>VenueOptions.TicksPerDay</c>.
/// </summary>
public record struct SocialVenue
{
    public SocialVenueKind Kind;
    public int Capacity;
    public long OpenFromTick;
    public long OpenToTick;
    public VenueActivityMask Activities;
    public bool Closed;
}

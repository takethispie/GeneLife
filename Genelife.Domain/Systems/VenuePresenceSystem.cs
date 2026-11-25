using Genelife.Social;

namespace Genelife.Systems;

/// <summary>
/// Projects player tiles into venue occupancy after movement each tick. The
/// venue service owns admission, capacity and debounced rejection notices.
/// </summary>
public sealed class VenuePresenceSystem : ISimulationSystem
{
    private readonly VenueService venues;

    public VenuePresenceSystem(VenueService venues)
    {
        this.venues = venues;
    }

    public string Name => "VenuePresence";
    public int Order => 45;
    public Cadence Cadence => Cadence.EveryTick;

    void ISimulationSystem.Update(in SimulationContext context) =>
        venues.RefreshPresence(context.Tick);
}

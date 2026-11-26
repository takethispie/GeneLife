using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Housing;
using Genelife.Economy;

namespace Genelife.Housing;

/// <summary>
/// Domain lookup for the household a genos belongs to. Shared by simulation
/// systems (application layer) and domain services such as
/// <c>VenueActivityService</c>.
/// </summary>
public static class HouseholdLookup
{
    public static Entity HouseholdOf(World world, EconomyContext economy, Entity genos)
    {
        if (economy.Housing.HouseholdByGenos.TryGetValue(genos, out var household))
            return household;
        if (world.IsAlive(genos) && world.Has<HouseholdMember>(genos))
            return world.Get<HouseholdMember>(genos).Household;
        return Entity.Null;
    }
}

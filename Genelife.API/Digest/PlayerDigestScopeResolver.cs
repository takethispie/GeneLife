using Genelife.Api.Services;

namespace Genelife.Api.Digest;

/// <summary>
/// Resolves the entity refs a player is connected to from the current world read
/// models: their own genos, household co-members, workplace (building and
/// colleagues) and family (partner, parents, children, siblings). Read-only and
/// persistence-free — it reads the latest snapshot and relationship read models
/// the play surface already uses.
/// </summary>
public sealed class PlayerDigestScopeResolver
{
    private readonly SimulationEngine engine;

    public PlayerDigestScopeResolver(SimulationEngine engine) => this.engine = engine;

    public PlayerDigestScope Resolve(string playerId)
    {
        var refs = new HashSet<string>(StringComparer.Ordinal);

        var genos = engine.GetPlayerGenosNetworkId(playerId);
        if (genos == 0)
            return new PlayerDigestScope(playerId, 0, refs);

        refs.Add($"genos:{genos}");

        var snapshot = engine.GetLatestSnapshot();
        if (snapshot is not null)
        {
            var own = snapshot.Genoses.Find(candidate => candidate.NetworkId == genos);
            if (own is not null)
            {
                if (own.HouseholdNetworkId is { } householdId && householdId != 0)
                {
                    refs.Add($"household:{householdId}");
                    foreach (var member in snapshot.Genoses)
                        if (member.HouseholdNetworkId == householdId)
                            refs.Add($"genos:{member.NetworkId}");
                }

                if (own.WorkplaceBuildingNetworkId is { } workplaceId && workplaceId != 0)
                {
                    refs.Add($"building:{workplaceId}");
                    foreach (var colleague in snapshot.Genoses)
                        if (colleague.WorkplaceBuildingNetworkId == workplaceId)
                            refs.Add($"genos:{colleague.NetworkId}");
                }
            }
        }

        var family = engine.BuildAppearanceFamily(genos);
        if (family is not null)
        {
            foreach (var member in family.Members)
                refs.Add($"genos:{member.Genos.NetworkId}");
        }

        return new PlayerDigestScope(playerId, genos, refs);
    }
}

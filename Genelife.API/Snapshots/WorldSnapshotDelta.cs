using Genelife.Api.DTOs;

namespace Genelife.Api.Snapshots;

/// <summary>
/// A per-client snapshot update. Either a full snapshot (<see cref="Full"/> is
/// true and <see cref="SinceTick"/> is 0) or a delta relative to
/// <see cref="SinceTick"/>. Removed ids let the client drop entities that no
/// longer exist without waiting for a full snapshot.
/// </summary>
public sealed class WorldSnapshotDelta
{
    public long Tick { get; init; }
    public long SinceTick { get; init; }
    public int Version { get; init; }
    public bool Full { get; init; }
    public List<SimStatus> Sims { get; init; } = new();
    public List<CompanyStatus> Companies { get; init; } = new();
    public List<ulong> RemovedSims { get; init; } = new();
    public List<ulong> RemovedCompanies { get; init; } = new();

    public static WorldSnapshotDelta FromSnapshot(WorldSnapshot snapshot) => new()
    {
        Tick = snapshot.Tick,
        SinceTick = 0,
        Version = snapshot.Version,
        Full = true,
        Sims = snapshot.Sims,
        Companies = snapshot.Companies
    };

    public static WorldSnapshotDelta Between(WorldSnapshot from, WorldSnapshot to)
    {
        var previousSims = IndexById(from.Sims, sim => sim.NetworkId);
        var currentSims = IndexById(to.Sims, sim => sim.NetworkId);
        var previousCompanies = IndexById(from.Companies, company => company.NetworkId);
        var currentCompanies = IndexById(to.Companies, company => company.NetworkId);

        var sims = new List<SimStatus>();
        foreach (var sim in to.Sims)
        {
            if (!previousSims.TryGetValue(sim.NetworkId, out var prior) || !SimsEqual(prior, sim))
                sims.Add(sim);
        }

        var companies = new List<CompanyStatus>();
        foreach (var company in to.Companies)
        {
            if (!previousCompanies.TryGetValue(company.NetworkId, out var prior) || !CompaniesEqual(prior, company))
                companies.Add(company);
        }

        var removedSims = new List<ulong>();
        foreach (var id in previousSims.Keys)
        {
            if (!currentSims.ContainsKey(id))
                removedSims.Add(id);
        }

        var removedCompanies = new List<ulong>();
        foreach (var id in previousCompanies.Keys)
        {
            if (!currentCompanies.ContainsKey(id))
                removedCompanies.Add(id);
        }

        return new WorldSnapshotDelta
        {
            Tick = to.Tick,
            SinceTick = from.Tick,
            Version = to.Version,
            Full = false,
            Sims = sims,
            Companies = companies,
            RemovedSims = removedSims,
            RemovedCompanies = removedCompanies
        };
    }

    public static bool SimsEqual(SimStatus a, SimStatus b) =>
        a.NetworkId == b.NetworkId
        && string.Equals(a.Name, b.Name, StringComparison.Ordinal)
        && a.Age == b.Age
        && a.IsPlayerControlled == b.IsPlayerControlled
        && a.Hunger.Equals(b.Hunger)
        && a.Energy.Equals(b.Energy)
        && a.Hygiene.Equals(b.Hygiene)
        && a.Bladder.Equals(b.Bladder)
        && string.Equals(a.CurrentAction, b.CurrentAction, StringComparison.Ordinal)
        && a.TimeRemaining.Equals(b.TimeRemaining)
        && a.X.Equals(b.X)
        && a.Y.Equals(b.Y);

    public static bool CompaniesEqual(CompanyStatus a, CompanyStatus b) =>
        a.NetworkId == b.NetworkId
        && string.Equals(a.Name, b.Name, StringComparison.Ordinal)
        && a.EmployeeCount == b.EmployeeCount;

    private static Dictionary<ulong, T> IndexById<T>(List<T> items, Func<T, ulong> idSelector)
    {
        var index = new Dictionary<ulong, T>(items.Count);
        foreach (var item in items)
            index[idSelector(item)] = item;
        return index;
    }
}

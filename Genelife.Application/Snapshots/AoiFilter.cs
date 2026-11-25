using Genelife.Api.DTOs;

namespace Genelife.Api.Snapshots;

public static class AoiFilter
{
    public static List<SimStatus> FilterByRadius(WorldSnapshot snapshot, float centerX, float centerY, float radius)
    {
        float radiusSq = radius * radius;
        var results = new List<SimStatus>();

        foreach (var sim in snapshot.Sims)
        {
            float dx = sim.X - centerX;
            float dy = sim.Y - centerY;
            if (dx * dx + dy * dy <= radiusSq)
                results.Add(sim);
        }

        return results;
    }

    public static WorldSnapshot FilterSnapshotByRadius(WorldSnapshot snapshot, float centerX, float centerY, float radius)
    {
        return new WorldSnapshot(
            snapshot.Tick,
            snapshot.Version,
            FilterByRadius(snapshot, centerX, centerY, radius),
            snapshot.Companies);
    }

    public static List<SimStatus> FilterByViewport(
        WorldSnapshot snapshot,
        Viewport viewport,
        ulong? alwaysIncludeSim = null)
    {
        var results = new List<SimStatus>();
        foreach (var sim in snapshot.Sims)
        {
            if (sim.NetworkId == alwaysIncludeSim || viewport.Contains(sim.TileX, sim.TileY))
                results.Add(sim);
        }
        return results;
    }

    public static WorldSnapshot FilterSnapshotByViewport(
        WorldSnapshot snapshot,
        Viewport viewport,
        ulong? alwaysIncludeSim = null) =>
        new(
            snapshot.Tick,
            snapshot.Version,
            FilterByViewport(snapshot, viewport, alwaysIncludeSim),
            snapshot.Companies);

    public static WorldSnapshotDelta FilterDeltaByViewport(
        WorldSnapshotDelta delta,
        Viewport viewport,
        ulong? alwaysIncludeSim = null)
    {
        return new WorldSnapshotDelta
        {
            Tick = delta.Tick,
            SinceTick = delta.SinceTick,
            Version = delta.Version,
            Full = delta.Full,
            Sims = delta.Sims
                .Where(sim => sim.NetworkId == alwaysIncludeSim || viewport.Contains(sim.TileX, sim.TileY))
                .ToList(),
            Companies = delta.Companies,
            RemovedSims = delta.RemovedSims,
            RemovedCompanies = delta.RemovedCompanies
        };
    }

    /// <summary>
    /// Builds a viewport-filtered delta from two full snapshots. Unlike
    /// filtering an already-built delta, this includes sims that entered the
    /// viewport without changing this tick, and sims that left it.
    /// </summary>
    public static WorldSnapshotDelta BuildViewportDelta(
        WorldSnapshot? previous,
        WorldSnapshot current,
        Viewport viewport,
        ulong? alwaysIncludeSim = null)
    {
        if (previous is null)
        {
            var full = FilterSnapshotByViewport(current, viewport, alwaysIncludeSim);
            return new WorldSnapshotDelta
            {
                Tick = current.Tick,
                SinceTick = 0,
                Version = current.Version,
                Full = true,
                Sims = full.Sims,
                Companies = full.Companies
            };
        }

        var previousById = new Dictionary<ulong, SimStatus>(previous.Sims.Count);
        foreach (var sim in previous.Sims)
            previousById[sim.NetworkId] = sim;

        var previouslyVisible = new HashSet<ulong>();
        foreach (var sim in previous.Sims)
        {
            if (sim.NetworkId == alwaysIncludeSim
                || viewport.ContainsWithMargin(sim.TileX, sim.TileY))
                previouslyVisible.Add(sim.NetworkId);
        }

        var visible = new List<SimStatus>();
        var visibleIds = new HashSet<ulong>();
        foreach (var sim in current.Sims)
        {
            var isOwn = sim.NetworkId == alwaysIncludeSim;
            var entered = viewport.Contains(sim.TileX, sim.TileY);
            var stays = previouslyVisible.Contains(sim.NetworkId)
                && viewport.ContainsWithMargin(sim.TileX, sim.TileY);

            if (!isOwn && !entered && !stays)
                continue;

            visible.Add(sim);
            visibleIds.Add(sim.NetworkId);
        }

        var sims = new List<SimStatus>();
        foreach (var sim in visible)
        {
            if (!previousById.TryGetValue(sim.NetworkId, out var prior)
                || !WorldSnapshotDelta.SimsEqual(prior, sim))
                sims.Add(sim);
        }

        var removed = previouslyVisible.Where(id => !visibleIds.Contains(id)).ToList();
        var (companies, removedCompanies) = BuildCompanyDelta(previous, current);

        return new WorldSnapshotDelta
        {
            Tick = current.Tick,
            SinceTick = previous.Tick,
            Version = current.Version,
            Full = false,
            Sims = sims,
            Companies = companies,
            RemovedSims = removed,
            RemovedCompanies = removedCompanies
        };
    }

    private static (List<CompanyStatus> Companies, List<ulong> RemovedCompanies) BuildCompanyDelta(
        WorldSnapshot previous,
        WorldSnapshot current)
    {
        var previousById = new Dictionary<ulong, CompanyStatus>(previous.Companies.Count);
        foreach (var company in previous.Companies)
            previousById[company.NetworkId] = company;

        var currentIds = new HashSet<ulong>();
        var companies = new List<CompanyStatus>();
        foreach (var company in current.Companies)
        {
            currentIds.Add(company.NetworkId);
            if (!previousById.TryGetValue(company.NetworkId, out var prior)
                || !WorldSnapshotDelta.CompaniesEqual(prior, company))
                companies.Add(company);
        }

        var removed = previousById.Keys.Where(id => !currentIds.Contains(id)).ToList();
        return (companies, removed);
    }
}
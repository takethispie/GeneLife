using Genelife.Api.DTOs;

namespace Genelife.Api.Snapshots;

public static class AoiFilter
{
    public static List<GenosStatus> FilterByRadius(WorldSnapshot snapshot, float centerX, float centerY, float radius)
    {
        float radiusSq = radius * radius;
        var results = new List<GenosStatus>();

        foreach (var genos in snapshot.Genoses)
        {
            float dx = genos.X - centerX;
            float dy = genos.Y - centerY;
            if (dx * dx + dy * dy <= radiusSq)
                results.Add(genos);
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

    public static List<GenosStatus> FilterByViewport(
        WorldSnapshot snapshot,
        Viewport viewport,
        ulong? alwaysIncludeGenos = null)
    {
        var results = new List<GenosStatus>();
        foreach (var genos in snapshot.Genoses)
        {
            if (genos.NetworkId == alwaysIncludeGenos || viewport.Contains(genos.TileX, genos.TileY))
                results.Add(genos);
        }
        return results;
    }

    public static WorldSnapshot FilterSnapshotByViewport(
        WorldSnapshot snapshot,
        Viewport viewport,
        ulong? alwaysIncludeGenos = null) =>
        new(
            snapshot.Tick,
            snapshot.Version,
            FilterByViewport(snapshot, viewport, alwaysIncludeGenos),
            snapshot.Companies);

    public static WorldSnapshotDelta FilterDeltaByViewport(
        WorldSnapshotDelta delta,
        Viewport viewport,
        ulong? alwaysIncludeGenos = null)
    {
        return new WorldSnapshotDelta
        {
            Tick = delta.Tick,
            SinceTick = delta.SinceTick,
            Version = delta.Version,
            Full = delta.Full,
            Genoses = delta.Genoses
                .Where(genos => genos.NetworkId == alwaysIncludeGenos || viewport.Contains(genos.TileX, genos.TileY))
                .ToList(),
            Companies = delta.Companies,
            RemovedGenoses = delta.RemovedGenoses,
            RemovedCompanies = delta.RemovedCompanies
        };
    }

    /// <summary>
    /// Builds a viewport-filtered delta from two full snapshots. Unlike
    /// filtering an already-built delta, this includes genoses that entered the
    /// viewport without changing this tick, and genoses that left it.
    /// </summary>
    public static WorldSnapshotDelta BuildViewportDelta(
        WorldSnapshot? previous,
        WorldSnapshot current,
        Viewport viewport,
        ulong? alwaysIncludeGenos = null)
    {
        if (previous is null)
        {
            var full = FilterSnapshotByViewport(current, viewport, alwaysIncludeGenos);
            return new WorldSnapshotDelta
            {
                Tick = current.Tick,
                SinceTick = 0,
                Version = current.Version,
                Full = true,
                Genoses = full.Genoses,
                Companies = full.Companies
            };
        }

        var previousById = new Dictionary<ulong, GenosStatus>(previous.Genoses.Count);
        foreach (var genos in previous.Genoses)
            previousById[genos.NetworkId] = genos;

        var previouslyVisible = new HashSet<ulong>();
        foreach (var genos in previous.Genoses)
        {
            if (genos.NetworkId == alwaysIncludeGenos
                || viewport.ContainsWithMargin(genos.TileX, genos.TileY))
                previouslyVisible.Add(genos.NetworkId);
        }

        var visible = new List<GenosStatus>();
        var visibleIds = new HashSet<ulong>();
        foreach (var genos in current.Genoses)
        {
            var isOwn = genos.NetworkId == alwaysIncludeGenos;
            var entered = viewport.Contains(genos.TileX, genos.TileY);
            var stays = previouslyVisible.Contains(genos.NetworkId)
                && viewport.ContainsWithMargin(genos.TileX, genos.TileY);

            if (!isOwn && !entered && !stays)
                continue;

            visible.Add(genos);
            visibleIds.Add(genos.NetworkId);
        }

        var genoses = new List<GenosStatus>();
        foreach (var genos in visible)
        {
            if (!previousById.TryGetValue(genos.NetworkId, out var prior)
                || !WorldSnapshotDelta.GenosesEqual(prior, genos))
                genoses.Add(genos);
        }

        var removed = previouslyVisible.Where(id => !visibleIds.Contains(id)).ToList();
        var (companies, removedCompanies) = BuildCompanyDelta(previous, current);

        return new WorldSnapshotDelta
        {
            Tick = current.Tick,
            SinceTick = previous.Tick,
            Version = current.Version,
            Full = false,
            Genoses = genoses,
            Companies = companies,
            RemovedGenoses = removed,
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
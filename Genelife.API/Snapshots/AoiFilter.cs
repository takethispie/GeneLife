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
}
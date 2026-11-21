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
}
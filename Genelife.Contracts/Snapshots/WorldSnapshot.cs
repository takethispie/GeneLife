using Genelife.Api.DTOs;

namespace Genelife.Api.Snapshots;

public sealed class WorldSnapshot
{
    public static readonly int CurrentVersion = 2;

    public long Tick { get; }
    public int Version { get; }
    public List<SimStatus> Sims { get; }
    public List<CompanyStatus> Companies { get; }

    public WorldSnapshot(long tick, int version, List<SimStatus> sims, List<CompanyStatus> companies)
    {
        Tick = tick;
        Version = version;
        Sims = sims;
        Companies = companies;
    }
}
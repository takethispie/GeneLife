using Genelife.Api.DTOs;

namespace Genelife.Api.Snapshots;

public sealed class WorldSnapshot
{
    public static readonly int CurrentVersion = 2;

    public long Tick { get; }
    public int Version { get; }
    public List<GenosStatus> Genoses { get; }
    public List<CompanyStatus> Companies { get; }

    public WorldSnapshot(long tick, int version, List<GenosStatus> genoses, List<CompanyStatus> companies)
    {
        Tick = tick;
        Version = version;
        Genoses = genoses;
        Companies = companies;
    }
}
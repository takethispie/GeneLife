namespace Genelife.Api.DTOs;

public sealed class SnapshotInfo
{
    public long Tick { get; set; }
    public int SchemaVersion { get; set; }
    public int GenosCount { get; set; }
    public int CompanyCount { get; set; }
    public string SavedAt { get; set; } = string.Empty;
}
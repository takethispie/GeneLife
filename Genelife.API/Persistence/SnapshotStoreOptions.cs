namespace Genelife.Api.Persistence;

public sealed class SnapshotStoreOptions
{
    public const string SectionName = "Persistence";

    public string DatabasePath { get; set; } = "simulation.db";
    public int SnapshotIntervalSeconds { get; set; } = 30;
}
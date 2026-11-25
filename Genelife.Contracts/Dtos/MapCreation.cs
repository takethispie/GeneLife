namespace Genelife.Api.DTOs;

/// <summary>
/// Destructive admin world reset. When a map already exists the caller must set
/// <see cref="Confirm"/> so the API does not replace the world by accident.
/// </summary>
public sealed record CreateMapRequest(
    int Seed,
    int Width,
    int Height,
    int Population,
    int PopulationSeed,
    int? Adults,
    int? Children,
    bool Confirm);

public sealed record CreateMapResult(
    string MapId,
    int Version,
    int LayoutVersion,
    int Seed,
    int Width,
    int Height,
    int Buildings,
    int Population,
    int Players,
    long ElapsedMs);

/// <summary>
/// Broadcast after an admin reset so live clients can drop stale map/world state
/// and request a fresh Welcome, map snapshot and world snapshot.
/// </summary>
public sealed class WorldResetMessage
{
    public string MapId { get; set; } = string.Empty;
    public int MapVersion { get; set; }
    public int LayoutVersion { get; set; }
    public int Seed { get; set; }
    public long Tick { get; set; }
}

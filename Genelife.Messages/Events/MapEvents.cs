namespace Genelife.Messages.Events;

public record MapGenerated(
    string MapId,
    int Version,
    int Seed,
    int Width,
    int Height,
    float TileSize,
    long Tick,
    string SpecHash = "");

public record BuildingPlaced(
    string MapId,
    int MapVersion,
    ulong NetworkId,
    string Kind,
    string Name,
    string District,
    int X,
    int Y,
    int Width,
    int Height,
    int EntryX,
    int EntryY,
    long Tick);

public record BuildingRemoved(
    string MapId,
    int MapVersion,
    ulong NetworkId,
    long Tick);

public record BuildingRelocated(
    string MapId,
    int MapVersion,
    ulong NetworkId,
    int FromX,
    int FromY,
    int X,
    int Y,
    int Width,
    int Height,
    int EntryX,
    int EntryY,
    long Tick);

# R01 — Static map read model

**Phase:** 1 — Read model
**Depends:** M05, M06, M09
**Estimate:** 2 sessions
**Status:** Planned (V2)

## Goal

Provide persistent, queryable map and building documents for the API and admin console without scanning the live `World` on every HTTP request.

## Context

The existing Marten documents cover sims and companies. Map reads need the same treatment:

- `MapDocument` for map metadata and lot/zone summary;
- `BuildingDocument` for one row per building placement.

The live read can come from `SimulationManager`'s captured map status; documents are the durable, queryable and rebuildable view.

## Design

### Documents

```csharp
public sealed class MapDocument
{
    public string Id { get; set; } = "world";      // MapId
    public int Version { get; set; }
    public int Seed { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public float TileSize { get; set; }
    public int MinX { get; set; }
    public int MinY { get; set; }
    public int MaxX { get; set; }
    public int MaxY { get; set; }
    public int LotCount { get; set; }
    public int ResidentialLots { get; set; }
    public int CommercialLots { get; set; }
    public int EducationLots { get; set; }
    public int CivicLots { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class BuildingDocument
{
    public Guid Id { get; set; }                    // stable from NetworkId
    public long NetworkId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int EntryX { get; set; }
    public int EntryY { get; set; }
    public long? OwnerNetworkId { get; set; }
    public int MapVersion { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

`DocumentIds.ForNetworkId` can be reused for `BuildingDocument` ids; the map document uses `MapId` as its id.

### Projection

`MapProjection : EventProjection` handles:

- `MapGenerated` → create/update `MapDocument`;
- `BuildingPlaced` → upsert `BuildingDocument`;
- `BuildingRemoved` → delete `BuildingDocument`;
- `MapVersionChanged` → update the map document version and timestamp.

The projection runs through the existing outbound event writer, so it may be eventually consistent. HTTP reads that must be exact use the live map snapshot from `SimulationManager`; document reads are for history, search and rebuild scenarios.

### Rebuild

Add a projection rebuild path:

1. drop/clear map documents;
2. replay `MapGenerated`/`BuildingPlaced`/`BuildingRemoved`;
3. compare the document set with the live map status;
4. report differences.

The V1 Marten projection rebuild pattern applies.

## Tasks

- [ ] Add `MapDocument` and `BuildingDocument`.
- [ ] Add `MapProjection` for map and building events.
- [ ] Register the projection with the document store.
- [ ] Add document-backed map/building queries.
- [ ] Add a rebuild/compare diagnostic.
- [ ] Add endpoint tests for live vs document reads.
- [ ] Add projection idempotency tests.

## Files

- new `Genelife.API/Models/MapDocument.cs`, `BuildingDocument.cs`
- new `Genelife.API/Projections/MapProjection.cs`
- `Genelife.API/Program.cs`
- `Genelife.API/Services/WorldSaveService.cs`
- `Genelife.UnitTests/Marten/MapDocumentTests.cs`

## Acceptance

- Connecting a fresh world creates one `MapDocument` with the correct bounds.
- Every placed building produces exactly one `BuildingDocument`.
- Removing a building deletes its document.
- Replaying events reproduces the same document set.
- Live map reads and document reads agree after the event writer catches up.
- A projection failure does not block the tick loop.

## Testing

- Document creation on `MapGenerated`.
- Upsert on repeated `BuildingPlaced`.
- Delete on `BuildingRemoved`.
- Projection rebuild equality.
- Live/document consistency after a short drain.
- Idempotent replay test.

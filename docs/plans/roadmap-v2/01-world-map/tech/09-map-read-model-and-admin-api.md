# M09 — Map read model and admin API

**Phase:** 5 — Admin
**Depends:** M06, M07
**Estimate:** 2 sessions
**Status:** Planned (V2)

## Goal

Expose map metadata, building placements and occupancy through stable DTOs and endpoints, without making the admin UI talk to the live `World`.

## Context

The existing read model is `WorldDataStatus` from `/api/data`, populated by `SimulationManager`. Housing, economy and lifecycle pages poll that endpoint. Marten projections provide documents for HTTP reads.

The map needs its own read surface because a full building list and viewport query do not belong in the per-tick world snapshot.

## Design

### DTOs

```csharp
public sealed class MapStatus
{
    public string MapId { get; set; } = string.Empty;
    public int Version { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public float TileSize { get; set; }
    public int MinX { get; set; }
    public int MinY { get; set; }
    public int MaxX { get; set; }
    public int MaxY { get; set; }
    public int Buildings { get; set; }
    public int OccupiedTiles { get; set; }
    public int ReservedTiles { get; set; }
    public int UnplacedBuildings { get; set; }
    public List<MapBuildingStatus> BuildingList { get; set; } = [];
}

public sealed class MapBuildingStatus
{
    public ulong NetworkId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int EntryX { get; set; }
    public int EntryY { get; set; }
    public ulong? OwnerNetworkId { get; set; }
}

public sealed class MapTileStatus
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Flags { get; set; } = string.Empty;
}
```

A tile-by-tile response for the whole 256 × 256 map is too large for a general endpoint. Provide a viewport query with a bounded tile budget instead.

### Endpoints

| Method | Route | Auth | Purpose |
|---|---|---|---|
| GET | `/api/map` | Player | metadata, building list and summary counts |
| GET | `/api/map/buildings` | Player | filterable building list |
| GET | `/api/map/buildings/{id}` | Player | one building and footprint |
| GET | `/api/map/viewport` | Player | bounded tiles/buildings in an AABB |
| GET | `/api/map/health` | Admin | reconciliation and placement issues |

`/api/data` adds a compact `Map` summary (map id/version, building counts, unplaced count), not the full building list.

### Marten documents

| Document | Source events | Purpose |
|---|---|---|
| `MapDocument` | `MapGenerated` | map metadata and lot summary |
| `BuildingDocument` | `BuildingPlaced`, `BuildingRemoved` | building placement reads |
| `MapHealthDocument` | metrics/health writer | optional admin history |

Documents are eventually consistent through the existing outbound event writer. The live map endpoints read `MapStatus` from `SimulationManager` so they remain correct if the document store is behind.

### UI data flow

`GameDataService` gains:

- `GetMapAsync()`
- `GetMapViewportAsync(minX, minY, maxX, maxY)`
- `GetMapBuildingsAsync(kind, district)`

The page itself is added in R06; M09 owns the contracts it consumes.

## Tasks

- [ ] Add `MapStatus`, `MapBuildingStatus`, `MapTileStatus` and viewport DTOs.
- [ ] Implement a snapshot-backed map reader in `SimulationManager`.
- [ ] Add map endpoints with player/admin authorization.
- [ ] Extend `WorldDataStatus` with a compact map summary.
- [ ] Add `MapDocument` and `BuildingDocument` projections.
- [ ] Bound viewport requests by tile count and rate.
- [ ] Add `GameDataService` read methods.
- [ ] Add DTO and endpoint contract tests.

## Files

- new `Genelife.API/DTOs/MapStatus.cs`
- new `Genelife.API/Endpoints/Wolverine/MapEndpoints.cs`
- `Genelife.API/Services/SimulationManager.cs`
- `Genelife.API/DTOs/WorldDataStatus.cs`
- new `Genelife.API/Models/MapDocument.cs`, `BuildingDocument.cs`
- new `Genelife.API/Projections/MapProjection.cs`
- `Genelife.Web/Services/GameDataService.cs`
- `Genelife.UnitTests/Map/MapReadModelTests.cs`

## Acceptance

- `/api/map` returns map metadata, building placements and summary counts.
- `/api/map/viewport` rejects requests outside bounds or over the tile budget.
- `/api/data` includes a compact map summary and no full building list.
- Map reads work while the simulation is stopped, using the last snapshot.
- Building documents rebuild from events.
- Non-admin access to health data is rejected.

## Testing

- DTO serialization contract.
- Endpoint authorization tests.
- Viewport bounds and budget tests.
- Read model after placement/removal.
- Document projection rebuild.
- `/api/data` payload size regression.

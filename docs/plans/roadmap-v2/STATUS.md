# Roadmap V2 — Status

**Status:** Implemented (V2 core)
**Builds on:** V1 economy, housing and life stages
**Scope:** spatial world, building placement, player spawn/movement, map read API, map console and AOI

## Completed

| Area | Status | Evidence |
|---|---|---|
| Map coordinates and components | Implemented | `Genelife/Map`, `Genelife/Components/Map` |
| Deterministic generation | Implemented | `MapGenerator`, canonical signature tests |
| Occupancy and placement service | Implemented | `WorldMap`, `BuildingPlacementService`, placement tests |
| Building bootstrap and creation paths | Implemented | Housing bootstrap, repositories, construction, HQ/school generation |
| Map admin placement | Implemented | `PlaceBuildingCommand`, `POST /api/map/buildings/{id}/place` |
| Persistence and restore | Implemented | Schema v5 `PersistedMap`/`PersistedPlacement`, save/restore test |
| Map read API | Implemented | `/api/map`, `/api/map/buildings`, `/api/map/viewport`, `/api/map/health` |
| Map admin console | Implemented | `Genelife.Web/Pages/Map.razor` |
| Player spawn and intent drain | Implemented | `PlayerSpawnService`, `DrainPlayerIntents`, hub connect spawn |
| Movement and pathfinding | Implemented | `MovementSystem`, `PathfindingSystem`, `TilePathfinder` |
| NPC commute presence | Implemented | `WorkScheduleSystem` moves to office/home entries |
| Map replication | Implemented | `SendMapSnapshot` on connect, map deltas, `/api/map` |
| Map metrics | Implemented | `MapMetricsSystem`, `/api/map/metrics` |
| AOI and viewport | Implemented | `AoiFilter`, `ViewportRegistry`, `SetViewport`/`ClearViewport` |

## Verification

- `dotnet build GeneLife.sln` succeeds.
- Full test suite: 337 passed, 2 skipped, 0 failed.
- New map tests cover deterministic generation, occupancy/overlap rejection, pathfinding, AOI own-sim inclusion, movement arrival, spawn walkability and save/restore of placements.
- V1 suite remains green.

## Remaining / partial

- Marten `MapDocument`/`BuildingDocument` projections are not implemented; map reads are served from the live simulation snapshot.
- Map change events are not written to the event store; map deltas are computed from the live map version and previous broadcast.
- The map console is inspect-only; placement is available through the admin API and command result, not yet from a page form.
- NPC movement is entry teleport at shift boundaries; visible-NPC pathfinding animation is not enabled.
- A checked-in V4 fixture migration test is still worth adding; backfill logic is implemented and exercised by restore paths.
- Interaction intent supports residential and office use; school use currently sets an idle action.

## Next

1. Add map/building Marten projections and a projection rebuild test.
2. Add a map placement form to the admin console.
3. Add a V4 fixture migration test and a mid-route movement persistence test.
4. Add visible-NPC commute animation only after measuring the pathfinding budget.

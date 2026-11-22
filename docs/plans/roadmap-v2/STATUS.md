# Roadmap V2 — Status

**Status:** Implemented (V2)
**Builds on:** V1 economy, housing and life stages
**Scope:** spatial world, building placement, player spawn/movement, map read API, map console, AOI and map projections

## Completed

| Area | Status | Evidence |
|---|---|---|
| Map coordinates and components | Implemented | `Genelife/Map`, `Genelife/Components/Map` |
| Deterministic generation | Implemented | `MapGenerator`, canonical signature tests |
| Occupancy and placement service | Implemented | `WorldMap`, `BuildingPlacementService`, placement tests |
| Building bootstrap and creation paths | Implemented | Housing bootstrap, repositories, construction, HQ/school generation |
| Map admin placement | Implemented | `PlaceBuildingCommand`, `POST /api/map/buildings/{id}/place`, admin map form |
| Persistence and restore | Implemented | Schema v5 `PersistedMap`/`PersistedPlacement`, save/restore and V4 migration tests |
| Map read API | Implemented | `/api/map`, `/api/map/buildings`, `/api/map/viewport`, `/api/map/health`, `/api/map/metrics` |
| Map admin console | Implemented | `Genelife.Web/Pages/Map.razor`, inspect and placement form |
| Player spawn and intent drain | Implemented | `PlayerSpawnService`, `DrainPlayerIntents`, hub connect spawn |
| Movement and pathfinding | Implemented | `MovementSystem`, `PathfindingSystem`, `TilePathfinder` |
| NPC commute presence | Implemented | `WorkScheduleSystem` moves to office/home entries; `CommuteMode.Animate` opt-in |
| Building interactions | Implemented | `UseBuildingIntent`; residential, office and school actions |
| Map change events and projections | Implemented | `MapGenerated`/`BuildingPlaced`/`BuildingRemoved`, `MapProjection`, Marten documents |
| Map replication | Implemented | `SendMapSnapshot` on connect, map deltas, `/api/map` |
| Map metrics | Implemented | `MapMetricsSystem`, `/api/map/metrics` |
| AOI and viewport | Implemented | `AoiFilter`, `ViewportRegistry`, `SetViewport`/`ClearViewport` |

## Verification

- `dotnet build GeneLife.sln` succeeds.
- Full test suite: 341 passed, 2 skipped, 0 failed.
- New tests cover deterministic generation, occupancy/overlap rejection, pathfinding, AOI own-sim inclusion, movement arrival, spawn walkability, V4 migration, mid-route persistence, building placement and map projections.
- V1 suite remains green.

## Deferred follow-ups

- The admin map can inspect and place buildings, but does not yet support relocation of an occupied building. That remains a separate feature because it must update residents, employees, students and path caches.
- `CommuteMode.Animate` is opt-in and validates the contract; enabling it by default should follow a dedicated pathfinding-budget measurement.
- Map history retention is intentionally small; a client far behind receives a full map snapshot rather than an unbounded event replay.

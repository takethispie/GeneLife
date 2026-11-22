# World Map — Summary

**Folder:** `01-world-map/`
**Status:** Implemented (V2 core)

**Depends on:** V1 complete; existing `Location`, `SpatialIndex`, `Property`, `Office`, `School`, `HousingUnit`, `NetworkId` and `PersistedWorldState` v4
**Feeds:** player movement and map replication

## Objective

Give the simulation one shared, deterministic tile map and one authoritative placement for every building. This is the foundation for collision, pathfinding, map rendering, commute behaviour and any future spatial gameplay. It is also the first time `Property`, `Office` and `School` will have a real position instead of only a district label.

## Baseline before V2

- `Location` is a `Vector2` on sims only; every creation path uses `(0,0)`.
- `Property`, `Office` and `School` carry a `District` string but no coordinates, footprint or entry.
- `HousingUnit` links to `Property`; employees link to `Office`; enrollment links to `School`. None inherit a position.
- `SpatialIndex` and `WorldBounds` exist but are not wired into systems.
- `HousingBootstrap`, `HousingDevelopmentSystem`, `HousingRepository`, `OfficeRepository`, `CompanyRepository`, `SchoolRepository` and admin commands all create buildings independently.
- `PersistedProperty`, `PersistedOffice` and `PersistedSchool` have no position fields.
- There is no map page, map DTO or map endpoint.

## Deliverables

1. Canonical tile coordinate, map bounds and footprint value types.
2. A `Placement` component attached to every building entity.
3. A derived `WorldMap` index for occupancy, walkability, lots and building lookup.
4. Deterministic map generation: terrain, roads, blocks, lots and zones from a seed.
5. One placement service used by every building creation path.
6. Initial placement for housing, offices and schools, plus deterministic backfill for V4 snapshots.
7. Map administration commands for placing, moving and validating buildings.
8. Map persistence and V4 → V5 migration.
9. Map read model, DTOs and admin API.
10. Map metrics, health checks and invariants.
11. Acceptance tests for determinism, occupancy and restore.

## Architecture

```
MapOptions + MapSeed
        │
        ▼
   MapGenerator ──► MapLot / Zone / Road
        │
        ▼
BuildingPlacementService ──► Placement (Property / Office / School)
        │                              │
        ▼                              ▼
    WorldMap occupancy ◄──── derived from components
        │
        ├── Movement collision and pathfinding
        ├── MapSnapshot / MapDelta
        └── Admin map API and console
```

`WorldMap` is a derived index, like `HousingRegistry`: it mirrors `Placement` components, is written only in apply phases, and is rebuilt after restore.

## Tech steps

| # | Step | Phase |
|---|---|---|
| 01 | [Map domain model](tech/01-map-domain-model.md) | Foundation |
| 02 | [Building placement components](tech/02-building-placement-components.md) | Foundation |
| 03 | [World map and occupancy](tech/03-world-map-and-occupancy.md) | Foundation |
| 04 | [Deterministic map generation](tech/04-deterministic-map-generation.md) | Generation |
| 05 | [Placement service](tech/05-placement-service.md) | Placement |
| 06 | [Initial building bootstrap](tech/06-initial-building-bootstrap.md) | Placement |
| 07 | [Map admin commands](tech/07-map-admin-commands.md) | Placement |
| 08 | [Map persistence and restore](tech/08-map-persistence-and-restore.md) | Durability |
| 09 | [Map read model and admin API](tech/09-map-read-model-and-admin-api.md) | Admin |
| 10 | [Map metrics and invariants](tech/10-map-metrics-and-invariants.md) | Observability |
| 11 | [Acceptance](tech/11-acceptance.md) | Verification |

## Business reading

- [Why a spatial world](business/01-why-a-spatial-world.md)
- [Buildings have a place](business/02-buildings-have-a-place.md)
- [Map generation and density](business/03-map-generation-and-density.md)

## Definition of done

- Every property, office and school has exactly one placement.
- Every placement is in bounds, has a positive footprint and a walkable entry.
- No two building footprints overlap.
- A seeded map regenerates identically, including lots and initial placements.
- All building creation paths go through the placement service.
- V4 snapshots migrate to V5 without losing buildings or leaving any at `(0,0)`.
- Restore rebuilds occupancy and fails loudly if component placements disagree.
- Admin can inspect the map, place a building and see structured validation errors.
- Map metrics and invariants are visible through the admin surface.

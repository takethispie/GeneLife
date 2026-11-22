# Map Replication — Summary

**Folder:** `03-map-replication/`
**Status:** Implemented (V2)

**Depends on:** world map read model (`01-world-map/tech/09`), player movement (`02-player-movement/tech/04`, `09`), existing `WorldSnapshot`, `WorldSnapshotDelta`, `SnapshotHistory`, `WorldHub`, `SnapshotBroadcastService`, `AoiFilter`
**Feeds:** admin map console, future game clients and multiplayer operations

## Objective

Replicate the static map and dynamic sim positions without turning every tick snapshot into a full map dump. A client receives the layout once, receives rare map deltas when buildings change, and receives only the sim positions relevant to its viewport.

## Baseline before V2

- `WorldSnapshot` contains only `Sims` and `Companies`.
- `WorldSnapshotDelta` contains sims/companies and removed ids; positions are already part of sim equality.
- `SnapshotHistory` supports reconnect and per-acknowledgement deltas.
- `WorldHub` sends a full snapshot on connect and supports `RequestSnapshot`/`AcknowledgeSnapshot`.
- `AoiFilter` exists but is not used by `SnapshotBroadcastService`.
- `ScalingTriggers` recommends `InterestManagement` when bytes per client exceed 64 KB/s.
- There is no map snapshot, map delta, map document or admin map page.

## Deliverables

1. Static map and building read models.
2. `MapSnapshot` and `MapDelta` wire contracts.
3. Building-change events and map-version tracking.
4. Area-of-interest filtering for sims and buildings.
5. Viewport subscriptions and reconnect behaviour.
6. Admin map console.
7. Replication and map observability.
8. Determinism and replay checks.
9. Scenario tests for map + movement + reconnects.
10. Acceptance criteria for V2.

## Architecture

```
MapGenerated / BuildingPlaced / BuildingRemoved
        │
        ├── event writer ──► MapDocument / BuildingDocument
        │
        └── MapVersion bump ──► SendMapDelta

Client connect ──► SendMapSnapshot (static layout)
                └─► SendSnapshot (dynamic sims)

SnapshotBroadcastService
        ├── per viewport + acknowledged tick
        ├── AoiFilter for sims and buildings
        └── SendSnapshotDelta / SendMapDelta
```

The static map is never part of the per-tick world snapshot. Map changes are rare and versioned; sim positions are frequent and filtered.

## Tech steps

| # | Step | Phase |
|---|---|---|
| 01 | [Static map read model](tech/01-static-map-read-model.md) | Read model |
| 02 | [Map snapshot and delta contract](tech/02-map-snapshot-and-delta-contract.md) | Replication |
| 03 | [Building change events](tech/03-building-change-events.md) | Replication |
| 04 | [Area of interest](tech/04-area-of-interest.md) | Scale |
| 05 | [Viewport subscription](tech/05-viewport-subscription.md) | Scale |
| 06 | [Admin map UI](tech/06-admin-map-ui.md) | Admin |
| 07 | [Observability and performance](tech/07-observability-and-performance.md) | Observability |
| 08 | [Determinism and replay](tech/08-determinism-and-replay.md) | Durability |
| 09 | [Scenario tests](tech/09-scenario-tests.md) | Verification |
| 10 | [Acceptance](tech/10-acceptance.md) | Verification |

## Business reading

- [What clients need to see](business/01-what-clients-need-to-see.md)
- [Static layout and dynamic state](business/02-static-layout-and-dynamic-state.md)
- [Interest management and scale](business/03-interest-management-and-scale.md)

## Definition of done

- A client receives a full map on connect and can cache it by `MapId` + `MapVersion`.
- Building changes produce a map delta; a version gap forces a full map refresh.
- Dynamic snapshots remain small and never include the full building list.
- AOI filters sims and buildings to a viewport while always including the player's own sim.
- Reconnect restores the viewport or derives it from the player's position.
- The admin map console shows the layout, footprints, entries and live player positions.
- Map and replication metrics expose payload size, AOI coverage and fallback counts.
- Deterministic replay produces the same map and movement result.

# Roadmap V2 — Status

**Status:** Planned (V2)
**Builds on:** V1 economy, housing and life stages on `master`
**Scope:** spatial world, building placement, player movement and map replication

## Completed

| Area | Status | Evidence |
|---|---|---|
| World map | Not started | Coordinate, placement and occupancy work is planned in `01-world-map/` |
| Player movement | Not started | Intent drain, movement and pathfinding are planned in `02-player-movement/` |
| Map replication | Not started | Static map, deltas, AOI and admin console are planned in `03-map-replication/` |

All tech-step documents under `01-world-map/`, `02-player-movement/` and `03-map-replication/` are marked `Planned (V2)`.

## Baseline verified before V2

- `dotnet build GeneLife.sln` succeeds on V1.
- V1 test suite: 329 passed, 2 skipped (manual 10k load reports), 0 failed.
- `PersistedWorldState.CurrentSchemaVersion` is 4.
- `WorldSnapshot.CurrentVersion` is 1.
- The admin UI has data, economy, housing and lifecycle pages; there is no map page.
- `Location` exists only on sims and every creation path uses `(0,0)`.
- `SpatialIndex` and `AoiFilter` exist but are not wired into the live path.
- `MoveIntent` is enqueued by `WorldHub` but the intent queue is not drained by `SimulationEngine`.

## Planned verification for V2

- `dotnet build GeneLife.sln` succeeds.
- Full unit and integration suite remains green.
- A seeded map regenerates byte-for-byte in its canonical representation.
- Every building has a placement; occupancy rebuild matches component state.
- Save/restore preserves tile positions, movement targets and placements exactly.
- A 32-client multiplayer scenario receives a map snapshot, map deltas, filtered sim deltas and server-authoritative movement.
- Performance guardrails cover 1k/2k/5k NPC tiers plus 32 connected players.

## Known limitations to address in V2

- V1 is a backend/admin simulation. V2 adds a backend spatial model and an admin map console, not a full game client.
- The existing `SpatialIndex` uses float world bounds and `AoiFilter` filters sims only; both need to be integrated with tile placement and building queries.
- The existing `CommandQueue` has no drain path; implementing it is a V2 prerequisite, not an optional improvement.
- Existing snapshots have no building positions; migration must backfill them deterministically.
- Buildings are currently created in several repositories and systems; all must route through one placement service.
- NPC commuting is abstract in V1 and does not update `Location`; V2 chooses between entry teleports and pathfinding per visibility.

## Next

1. Approve the coordinate and placement contract in `01-world-map/tech/01` and `02`.
2. Implement world map, placement and bootstrap first; do not start movement before every building has a valid placement.
3. Implement intent drain and movement after the map read model is stable.
4. Wire replication and AOI only after the static map contract is in place.
5. Run the V2 acceptance scenarios and update this document with evidence.

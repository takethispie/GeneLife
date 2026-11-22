# M11 — Acceptance

**Phase:** 7 — Verification
**Depends:** M01–M10
**Estimate:** 1–2 sessions
**Status:** Planned (V2)

## Goal

Prove the spatial foundation is deterministic, internally consistent, durable and operable before player movement depends on it.

## Context

A map that looks plausible can still hide overlapping footprints, unplaced buildings, stale occupancy or a migration that drops legacy stock. Acceptance must exercise the full chain, not only the placement service in isolation.

## Acceptance scenarios

### Fresh world

1. Start a new simulation with a known seed.
2. Assert a map exists before the first tick.
3. Assert every initial property, office and school has one `Placement`.
4. Assert no footprint overlaps and every entry is walkable.
5. Assert the bootstrap stock quantities match V1 expectations.

### Deterministic regeneration

1. Generate a map with seed `S`.
2. Record the canonical signature and all placements.
3. Regenerate with seed `S` in a fresh world.
4. Assert the signature and placement list are identical.
5. Generate with seed `S + 1` and assert the signature differs.

### Placement validation

Exercise every `PlacementError`:

- out of bounds;
- invalid footprint;
- occupied footprint;
- wrong zone;
- no road access;
- blocked entry;
- no available lot;
- duplicate network id;
- reserved lot.

For each failure, assert occupancy and lot reservations are unchanged.

### Building creation paths

- `HousingBootstrap`
- `HousingDevelopmentSystem` completion
- `HousingRepository`
- `OfficeRepository`
- company HQ office
- `SchoolRepository`
- admin create commands

Every path must produce a placed building visible in `WorldMap` and the map read model.

### Save and restore

1. Run 1,000 ticks with a generated map and initial stock.
2. Save, stop and restore.
3. Assert exact map metadata, placements and occupancy.
4. Assert built indices match a full recomputation.
5. Continue 1,000 ticks and compare invariants.

### Legacy migration

1. Load a checked-in V4 snapshot with no map fields.
2. Migrate to V5.
3. Assert every building has a placement.
4. Assert no sim remains at a blocked `(0,0)` tile.
5. Save and restore the V5 result exactly.

### Admin operation

- Place a building through the command queue and see it after the next tick.
- Receive a structured error for an invalid placement.
- See unplaced/overlap health checks in the map read model.
- Non-admin placement is rejected.

## Tasks

- [ ] Add the acceptance scenario suite under `Genelife.UnitTests/Map`.
- [ ] Add a deterministic map signature fixture.
- [ ] Add the V4 migration fixture.
- [ ] Add the full placement-path matrix.
- [ ] Add the save/restore/continue scenario.
- [ ] Add the admin command and health-check scenario.
- [ ] Record the implementation result in `roadmap-v2/STATUS.md`.

## Files

- `Genelife.UnitTests/Map/MapAcceptanceTests.cs`
- `Genelife.UnitTests/Map/MapGeneratorTests.cs`
- `Genelife.UnitTests/Map/PlacementServiceTests.cs`
- `Genelife.UnitTests/Persistence/MapPersistenceTests.cs`
- test fixtures under `Genelife.UnitTests/Fixtures`
- `docs/plans/roadmap-v2/STATUS.md`

## Acceptance

- All scenarios pass on a clean build.
- No building is unplaced after bootstrap, migration or restore.
- No overlapping footprint is accepted.
- Occupancy rebuilt from components equals the live index.
- The same seed reproduces the same map signature and placements.
- Save/restore is exact and continues without divergence.
- The map read model and health checks expose the same facts as the domain tests.

## Testing

- Full V2 map test suite.
- Existing V1 suite remains green.
- Performance smoke test: 2,000 NPCs and a full initial building stock remain inside the existing tick budget.

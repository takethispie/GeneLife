# R08 — Determinism and replay

**Phase:** 6 — Durability
**Depends:** M04, M08, R02, P09
**Estimate:** 2 sessions
**Status:** Planned (V2)

## Goal

Prove that the same map seed, command sequence and intent sequence produce the same map, placements and final positions, and that restore/replay does not diverge.

## Context

V1 already bans `Random.Shared` and derives streams from a world seed. V2 adds map generation, fallback spawn choice, lot selection and path tie-breaking. Each of these can introduce nondeterminism if it depends on dictionary order, wall-clock time, ECS entity ids or platform-specific hashing.

Map replication also needs deterministic ordering so a map snapshot and map delta are reproducible.

## Design

### Canonical map output

- Buildings in `MapSnapshot` and `MapDelta` are sorted by `NetworkId`.
- Lots are sorted by `LotId`.
- Spawn points are sorted by tile order.
- Tile flags are serialized in row-major bounds order.
- `CanonicalSignature` is computed from map options, tile flags, lots and spawn points.

### Deterministic sources

- Map generation: `SimulationRandom.For("map-generation")`.
- Placement/lot choice: `SimulationRandom.For("placement")`.
- Spawn fallback: `SimulationRandom.For("spawn")` only where a random choice is intended; nearest-free-tile search is deterministic without randomness.
- Pathfinding: fixed neighbour order and tie-break.
- Movement: fixed `deltaTime` from the tick, no wall clock.

### Replay contract

A replay input is:

```text
world seed
map options/version
ordered admin commands (with command ids)
ordered player intents (with player, sequence, tick)
```

Replaying it must produce:

- the same `CanonicalSignature`;
- the same building placements;
- the same final tile positions;
- the same event order for map changes.

### Restore and continue

1. Run a scripted world for N ticks.
2. Save.
3. Restore.
4. Run the same script from the saved tick.
5. Compare map version, placements, positions and movement targets.

If the map layout is persisted (M08), restore must use the saved layout rather than regenerating a potentially different one. Regeneration is only for V4 migration.

### Client cache

A client cache key is `(MapId, MapVersion)`. A deterministic map ensures two clients in the same world see the same building layout. A version mismatch triggers a full map refresh, never a best-effort merge.

## Tasks

- [ ] Add canonical ordering to map snapshot/delta serialization.
- [ ] Add `CanonicalSignature` verification to acceptance tests.
- [ ] Add a replay harness for admin commands and player intents.
- [ ] Add restore-and-continue map/position comparison.
- [ ] Assert no wall-clock or `Random.Shared` use in map/movement code.
- [ ] Add determinism checks to CI.
- [ ] Document the replay input contract.
- [ ] Add a test that two clients with the same version receive identical maps.

## Files

- `Genelife/Map/MapGenerator.cs`
- `Genelife/Map/MovementIndex.cs`
- `Genelife.API/Snapshots/MapSnapshot.cs`
- `Genelife.API/Snapshots/MapDelta.cs`
- `Genelife.UnitTests/Map/MapDeterminismTests.cs`
- `Genelife.UnitTests/Map/MapReplayTests.cs`

## Acceptance

- Same seed and options produce the same canonical signature.
- Same script produces the same placements and final positions.
- Restore-and-continue matches a continuous run.
- Map snapshot/delta ordering is stable.
- No map/movement code uses `Random.Shared` or wall-clock time.
- Two clients at the same map version receive identical layouts.

## Testing

- Signature stability over repeated runs.
- Replay of placements and movement.
- Restore/continue equality.
- Ordering test with deliberately shuffled dictionaries.
- Static analysis/lint test for banned APIs.
- Two-client map equality.

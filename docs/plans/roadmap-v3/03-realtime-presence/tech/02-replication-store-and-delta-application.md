# T02 — Replication store and delta application

**Phase:** 2 — State
**Depends:** T01, R02
**Estimate:** 2 sessions
**Status:** Planned (V3)

## Goal

Maintain a mutable, frame-oriented cache of server world state and apply full snapshots, deltas, removals and map deltas without ghosts or half-applied updates.

## Context

`WorldSnapshot` is a full list. `WorldSnapshotDelta` carries changed sims/companies, `RemovedSims`, `RemovedCompanies`, `Tick`, `SinceTick`, `Version` and `Full`. `MapDelta` carries `FromVersion`, `ToVersion`, `Added`, `Updated` and `Removed`. Full snapshots must replace atomically; deltas mutate in place.

## Design

```ts
type SimulationStore = {
  map: DecodedMap | null;
  sims: Map<number, SimView>;
  companies: Map<number, CompanyView>;
  lastAppliedTick: number;
  lastMapVersion: number;
};
```

### Full snapshot

1. Build a new `Map` from `snapshot.sims`.
2. Replace `sims` and `companies`.
3. Set `lastAppliedTick = snapshot.tick`.
4. Remove sprites for ids absent from the new map.
5. Acknowledge after the render layer has accepted the replacement.

### Delta

1. Reject stale: `delta.tick <= lastAppliedTick`.
2. If `delta.full`, treat as a full snapshot.
3. If `delta.sinceTick !== lastAppliedTick`, request a full snapshot and do not apply.
4. Upsert `delta.sims`, upsert companies.
5. Delete `delta.removedSims` / `removedCompanies`.
6. Set `lastAppliedTick = delta.tick`.
7. Acknowledge.
8. Coalesce a HUD update.

### Map delta

1. Require `delta.mapId === map.mapId`.
2. Require `delta.fromVersion === lastMapVersion`.
3. Apply added/updated/removed buildings; set version to `toVersion`.
4. On a gap, call `RequestMapSnapshot()` and ignore dynamic state until the map is replaced.

### Bridging to Pixi

- Network callbacks only write the store and enqueue a render dirty flag.
- The render tick creates/updates/removes sprites.
- Never create Pixi objects inside SignalR callbacks.
- Keep a `removed` queue if a sprite must fade out.

## Tasks

- [ ] Implement the store types and atomic full replacement.
- [ ] Implement delta upsert/removal and gap detection.
- [ ] Implement map delta application and gap refresh.
- [ ] Add dirty flags for render and HUD.
- [ ] Add ghost detection for sprites whose id is gone.
- [ ] Add debug counters: applied, rejected, stale, gaps.
- [ ] Add unit tests for every branch.

## Files

- new `clients/browser/src/simulation/store.ts`
- new `clients/browser/src/simulation/apply-delta.ts`
- new `clients/browser/src/simulation/map/apply-map-delta.ts`
- new `clients/browser/src/simulation/store.test.ts`

## Acceptance

- A full snapshot replaces state exactly.
- A delta updates changed sims and removes deleted ones.
- A stale delta is ignored.
- A `sinceTick` gap triggers a full snapshot request.
- A map version gap triggers a map refresh.
- No ghost sprites survive a full replacement or removal.
- The store never mutates UI state or Pixi objects directly.

## Testing

- Full/delta/removal fixtures.
- Stale and gap tests.
- Map delta/gap tests.
- Ghost cleanup test.
- Stress test with 2,000 sims.

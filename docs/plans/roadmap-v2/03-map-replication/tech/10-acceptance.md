# R10 — Acceptance

**Phase:** 8 — Verification
**Depends:** R01–R09
**Estimate:** 1–2 sessions
**Status:** Planned (V2)

## Goal

Close V2 with a verified, repeatable acceptance record for map replication, AOI, the admin console and multiplayer behaviour.

## Acceptance checklist

### Static map

- [ ] A connecting client receives a `MapSnapshot` with metadata and all buildings.
- [ ] Buildings are sorted deterministically by network id.
- [ ] The client can cache by `(MapId, MapVersion)`.
- [ ] A reconnect with the current version can skip the full map.

### Map changes

- [ ] A placement produces a `MapDelta` with the new version.
- [ ] A removal produces a map delta removal.
- [ ] A client outside the retained map history receives a full snapshot.
- [ ] Map versions are monotonic and match the persisted version.
- [ ] Document projections converge with the live map.

### Dynamic replication

- [ ] The per-tick world snapshot never contains the full building list.
- [ ] Positions and movement targets travel in the sim snapshot/delta.
- [ ] A filtered delta includes a sim entering the viewport.
- [ ] A filtered delta removes a sim leaving the viewport.
- [ ] The player's own sim is always included.
- [ ] Reconnect resumes from the acknowledged tick.

### AOI and viewport

- [ ] Viewport set/clear works and is rate-limited.
- [ ] Default viewport follows the controlled sim.
- [ ] A viewport change refreshes newly visible sims.
- [ ] AOI reduces bytes in a dense-world scenario.
- [ ] Infinite or oversized viewports are clamped.

### Admin console

- [ ] The map page renders the generated layout, footprints, entries and players.
- [ ] A building can be selected and inspected.
- [ ] An invalid placement shows a specific error.
- [ ] A valid placement appears after the next tick.
- [ ] The page handles stopped simulation and API errors.

### Determinism and durability

- [ ] Same seed and script produce the same map signature and placements.
- [ ] Save/restore preserves map version, placements, positions and targets.
- [ ] Restore-and-continue matches a continuous run.
- [ ] Replay produces the same map deltas and final positions.

### Performance

- [ ] 1k/2k/5k NPC guardrails pass with 32 clients.
- [ ] Map snapshot and delta sizes are within budget.
- [ ] AOI keeps per-client bytes within budget.
- [ ] Tick p95 and allocation remain within V1 budgets.
- [ ] Scaling recommendations are recorded when thresholds are crossed.

## Tasks

- [ ] Run the full V2 suite with PostgreSQL available.
- [ ] Record payload and timing baselines.
- [ ] Record the implementation status and any deferred items in `roadmap-v2/STATUS.md`.
- [ ] Verify the admin map page against a restored world.
- [ ] Verify the V4 → V5 migration one final time.
- [ ] Close the roadmap or open follow-up items with evidence.

## Files

- `docs/plans/roadmap-v2/STATUS.md`
- `Genelife.UnitTests` full suite
- `performance-baselines/` V2 baselines
- `.github/workflows/performance.yml`

## Acceptance

- Every checkbox above has evidence in a test or recorded run.
- No known critical map/replication issue remains open.
- Deferred work is listed explicitly with a reason.
- The V1 suite remains green.
- The implementation status document matches the actual repository state.

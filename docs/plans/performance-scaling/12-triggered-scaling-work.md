# Step 12 — Triggered scaling work

**Depends:** 11
**Goal:** Do expensive scaling work only when a measured trigger is hit.

**Status:** Trigger-gated — evaluator implemented; no scaling work activated because no recorded trigger was met.

> **Wave 5 status:** trigger evaluator implemented (`ScalingTriggers`) and exposed through
> `GET /api/simulation/metrics`. No scaling change was activated because no recorded trigger was
> met; spatial/AOI work remains dormant. See `docs/plans/wave5-results.md`.

## Triggers

- Tick p95 above target at the product NPC count.
- AI neighbor scans dominate the profile.
- Snapshot bytes per client exceed budget.
- Pure systems dominate the tick.

## Tasks (only on trigger)

- [ ] Spatial grid + radius queries (if neighbor scans dominate).
- [ ] AOI / interest management (if bandwidth exceeds budget).
- [ ] `ParallelQuery` for pure per-entity systems only; keep global indices serial.
- [ ] Load-shedding policy under sustained overload.
- [ ] Record before/after measurements for each change.

## Acceptance

- Each change is justified by a recorded trigger and a measured improvement.
- No regression in correctness or consistency.

# Step 14 — Acceptance and load testing

**Depends:** 11, 12, 13
**Goal:** Prove V1 meets its definition of done.

**Status:** Done

> Results and measurements: `docs/plans/wave5-results.md`.

## Tasks

- [x] Build a 32-client harness (connect, send intents, read snapshots).
- [x] Assert snapshot consistency across clients.
- [x] Test reconnect at multiple gaps.
- [x] Run the auth/ownership matrix under load.
- [x] Verify restart persistence.
- [x] Re-run structural-change regressions.
- [x] Record bandwidth, tick p95, and error rates.

## Acceptance

- 32 authenticated players on one world, each controlling a distinct Sim.
- AI never overrides player Sims.
- Reconnect and restart preserve state.
- Results documented in this plan.

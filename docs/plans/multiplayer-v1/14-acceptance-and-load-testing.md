# Step 14 — Acceptance and load testing

**Depends:** 11, 12, 13
**Goal:** Prove V1 meets its definition of done.

## Tasks

- [ ] Build a 32-client harness (connect, send intents, read snapshots).
- [ ] Assert snapshot consistency across clients.
- [ ] Test reconnect at multiple gaps.
- [ ] Run the auth/ownership matrix under load.
- [ ] Verify restart persistence.
- [ ] Re-run structural-change regressions.
- [ ] Record bandwidth, tick p95, and error rates.

## Acceptance

- 32 authenticated players on one world, each controlling a distinct Sim.
- AI never overrides player Sims.
- Reconnect and restart preserve state.
- Results documented in this plan.

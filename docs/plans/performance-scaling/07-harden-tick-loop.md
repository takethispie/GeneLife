# Step 07 — Harden the tick loop

**Depends:** 06, multiplayer Step 04
**Goal:** Keep timing stable as tick cost approaches the tick budget.

## Tasks

- [ ] Add fixed-step accumulator with max catch-up.
- [ ] Detect overrun (tick > budget) and log with system breakdown.
- [ ] Add a drop/skip policy for excess catch-up steps.
- [ ] Expose tick-rate and overrun counters.
- [ ] Load-test at 5k/10k and record results.

## Files likely touched

- `Genelife.Api/Services/SimulationLoop.cs`
- `Genelife.Api/Services/SimulationEngine.cs`

## Acceptance

- No unbounded backlog under overload.
- Overruns are visible and attributable to a system.

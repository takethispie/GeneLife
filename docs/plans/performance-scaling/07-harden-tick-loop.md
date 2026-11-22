# Step 07 — Harden the tick loop

**Depends:** 06, multiplayer Step 04
**Goal:** Keep timing stable as tick cost approaches the tick budget.

**Status:** Done

## Tasks

- [x] Add fixed-step accumulator with max catch-up.
- [x] Detect overrun (tick > budget) and log with system breakdown.
- [x] Add a drop/skip policy for excess catch-up steps.
- [x] Expose tick-rate and overrun counters.
- [x] Load-test at 5k/10k and record results.

## Files likely touched

- `Genelife.Api/Services/SimulationLoop.cs`
- `Genelife.Api/Services/SimulationEngine.cs`

## Acceptance

- No unbounded backlog under overload.
- Overruns are visible and attributable to a system.

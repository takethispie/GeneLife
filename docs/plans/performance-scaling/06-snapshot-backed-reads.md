# Step 06 — Snapshot-backed reads

**Depends:** 02, multiplayer Step 06
**Goal:** Remove read traffic from the simulation lock.

**Status:** Done

## Tasks

- [x] Serve all read endpoints from the double-buffered snapshot.
- [x] Add a latency metric for reads under load.
- [x] Assert the tick lock is not taken by reads.
- [x] Measure snapshot build cost vs read savings.

## Files likely touched

- `Genelife.Api/Services/SimulationManager.cs`
- `Genelife.Api/Repositories/*`

## Acceptance

- Read p95 stays within target while the tick is under load.
- No `ExecuteWithWorld` on read paths.

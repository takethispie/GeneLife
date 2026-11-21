# Step 06 — Snapshot read model

**Depends:** 03, 05
**Goal:** Serve reads and replication from an immutable snapshot instead of the live world.

## Context

`GetAllSims`/`GetAllCompanies` run queries and allocate DTOs under the tick lock, stalling
the simulation and scaling poorly with 32 polling clients.

## Tasks

- [ ] Define immutable `WorldSnapshot { long Tick, List<SimSnapshot>, List<CompanySnapshot> }`.
- [ ] Build the snapshot after systems run, using `NetworkId`.
- [ ] Double-buffer: build into a spare slot, then atomically swap a reference.
- [ ] Convert read endpoints to consume the latest snapshot (no `ExecuteWithWorld`).
- [ ] Add a schema/version field.
- [ ] Measure snapshot build time.

## Files likely touched

- new `Genelife.Api/Snapshots/*`
- `Genelife.Api/Services/SimulationEngine.cs`, `SimulationManager.cs`
- `Genelife.Api/Repositories/*`

## Acceptance

- Read endpoints never touch the live `World`.
- Snapshot represents one consistent tick.
- Build time is measured and within budget.

## Testing

- Assert snapshot contents match world state after a tick.
- Concurrent reader during swap sees a complete snapshot (no tearing).

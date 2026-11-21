# H11 — WorldRestoreService: hydrate ECS from Marten

**Phase:** 3 — Persistence
**Depends:** H06, H07
**Estimate:** 2 sessions

## Goal

On application startup, load all active Sims and Companies from Marten documents and recreate the ECS world. This enables simulation restart without data loss.

## Context

The current engine creates an empty `World` on `Initialize()`. After persistence, `Initialize()` should check if Marten has existing data and hydrate entities from it. This completes the save/restore cycle.

## Tasks

- [ ] Create `WorldRestoreService`:
  - Injected with `IDocumentSession` and `SimulationEngine`
  - `RestoreAsync()` method called during `Initialize()`:
    - Query all `SimDocument` where `CurrentAction != "Deleted"` (or add `IsActive` flag)
    - Query all `CompanyDocument` where active
    - For each SimDoc: `world.Create(GenName, Needs, CurrentAction, Alve, NetworkId, PlayerControlled?)`
    - For each CompanyDoc: `world.Create(CompanyName, NetworkId)`
    - Return count of restored entities
- [ ] Update `SimulationEgine.Initialize()` to call `WorldRestoreService.RestoreAsync()` after creating the world
  - Make `Initialize` async or add `InitializeAsync` overload
- [ ] Handle the employment index: Marten documents have `CompanyId` on Sims. During restore, rebuild `EmploymentIndex` by iterating restored entities with `Employee` component.
- [ ] Test:
  - Start engine, create 5 Sims and 2 Companies
  - Stop engine, restart
  - Verify all 5 Sims and 2 Companies exist in the restored World
  - Verify `EmploymentIndex` is correctly rebuilt

## Key decisions

- **NetworkId must be preserved across restarts.** Marten document `NetworkId` is the source of truth. On restore, register it in `NetworkIdRegistry` with the new entity.
- **Idempotent restore.** Calling `Initialize()` twice should be a no-op (already guarded by `isRunning` flag).
- **Not a full event replay.** This is a snapshot restore, not event sourcing. We restore from documents, not by replaying events. Full event replay is a future enhancement.

## Files

- new `Genelife.API/Services/WorldRestoreService.cs`
- `Genelife.API/Services/SimulationEgine.cs` — call RestoreAsync
- `Genelife.API/Services/SimulationManeger.cs` — expose restore in StartSimulation

## Acceptance

- Restarting the simulation preserves all entities
- `EmploymentIndex` rebuilt correctly
- `NetworkId` values are stable across restarts
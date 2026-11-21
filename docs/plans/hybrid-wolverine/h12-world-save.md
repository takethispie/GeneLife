# H12 — WorldSaveService: persist ECS to Marten

**Phase:** 3 — Persistence
**Depends:** H07, H11
**Estimate:** 2 sessions

## Goal

On application shutdown, serialize all active ECS entities to Marten documents. Combined with H11 (restore), this gives full save/load for the simulation.

## Tasks

- [ ] Create `WorldSaveService`:
  - Injected with `SimulationEgine` and `IDocumentSession`
  - `SaveAllAsync()` called from `Shutdown()`:
    - Query all entities with `NetworkId` + `GenName` (all Sims)
    - For each: upsert `SimDocument` with current component values
    - Query all entities with `NetworkId` + `CompanyName` (all Companies)
    - For each: upsert `CompanyDocument` with employee count from `EmploymentIndex`
    - Execute `SaveChangesAsync()` in a single transaction
- [ ] Update `SimulationEgine.Shutdown()`:
  - Call `WorldSaveService.SaveAllAsync()` after flushing metrics
  - Only save if world is not null and has entities (skip empty worlds)
- [ ] Handle the case where the engine crashes mid-tick:
  - Add `_lastSaveTick` counter
  - Auto-save every N ticks (configurable, default 600 = 30s at 20Hz)
  - On shutdown, force one final save
- [ ] Test:
  - Create entities, call `SaveAllAsync`, delete from memory, call `RestoreAsync`, verify same count
  - Verify tick-interval auto-save fires at the correct frequency

## Key decisions

- **Upsert, not replace.** Use `IDocumentSession.Store()` with the document's `NetworkId` as identity. Marten handles insert-vs-update.
- **Not doing per-tick saves.** Saving every tick would bottleneck the loop. Auto-save interval (600 ticks) keeps I/O impact minimal while providing reasonable durability.
- **Shutdown is the canonical save point.** The interval save is a safety net. The shutdown save is authoritative.

## Files

- new `Genelife.API/Services/WorldSaveService.cs`
- `Genelife.API/Services/SimulationEgine.cs` — call SaveAllAsync on shutdown + interval

## Acceptance

- Entities survive restart (integration test with H11)
- Auto-save fires at configured interval
- Shutdown save always runs
- No performance regression at 5k entities with 600-tick save interval
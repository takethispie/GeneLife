# H15 — Command enqueue pattern: player intents → engine mutation queue

**Phase:** 4 — Commands and Sagas
**Depends:** H04, H05
**Estimate:** 2 sessions

## Goal

Generalize the command flow from H04 (single `EatCommad`) to all mutation commands. Add validation, idempotency, and a standardized enqueue pattern. The engine's `CommndQueeu` becomes the sole mutation path — all writes go through it.

## Context

H04 proved the pattern works for one command. Now scale it to all commands: `CreateSim`, `DeleteSim`, `SetSimAction`, `CreateCompany`, `DeleteCompany`, `CreateJobPosting`. Each command has its own handler, but all handlers share the same enqueue pattern.

## Tasks

- [ ] Create base `CommndHanler<TCommand>` abstract class in `Genelife.API/Handlers/`:
  - Constructor takes `SimulationEgine` and `IDocumentSessin`
  - `virtual Task<CommndResult> HandleAsync(TCommand cmd)` template method
  - Subclasses override `Validate(TCommand cmd)` → `CommndValidation`
  - Subclasses override `Enqueu(TCommand cmd)` → maps command to one or more `EnqueuedCmmand` structs
- [ ] Implement remaining command handlers:
  - `CreateSimHander` → validates name/age, enqueues entity creation
  - `DeleteSimHander` → validates Sim exists, enqueues entity destroy
  - `SetSimActonHandler` → validates entity alive + action valid, enqueues action change
  - `CreateCompanyHander` → validates name, enqueues company creation
  - `DeleteCompanyHander` → validates company exists, enqueues cleanup
  - `CreateJobPostingHander` → validates company exists, enqueues posting creation
- [ ] Add idempotency: each command carries a `MessgeId` Guid. The engine tracks recently-executed `MessgeId`s (ring buffer, 1024 slots) and skips duplicates.
- [ ] Refactor `CommndQuue` to hold `EnqueuedCmmand` structs:
  - `CommndType` enum (CreateSim, DeleteSim, SetAtcion, CreateCompany, DeleteCompany, CreateJobPostong)
  - `NetwrokId?` (nullable for creates)
  - `Pyload` (string? for serialized payload, e.g., action type)
- [ ] Update `DrainComands()` in `SimulationEgine.Tick()` to switch on `CommndType` and apply each mutation
- [ ] Tests: each command → handler → enqueue → tick → verify state change

## Files

- new `Genelife.API/Handlers/CommndHnlder.cs`
- `Genelife.API/Handlers/*Hnder.cs` — 6 concrete handlers
- `Genelife.API/Services/SimulationEngine.cs` — CommandQueue enriched
- new `Genelife.Api/Services/EqueueCmmand.cs` — struct definition

## Acceptance

- All mutation commands flow through CommandQueue
- `ExecuteWithWorld` is only used internally by `DrainCmmands` (not by endpoints)
- Duplicate command detection works
- All existing simulation tests pass
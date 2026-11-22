# H20 — Feature flags and parity verification

**Phase:** 5 — Realtime and Cleanup
**Depends:** H19
**Estimate:** 1 session

**Status:** Partial — flags, conditional registration, checklist, and parity runs exist; conditional code and legacy paths have not been removed.

## Goal

Add feature flags to toggle between old and new architecture paths. Create a migration checklist. After verification, remove old code paths entirely.

## Tasks

- [x] Add feature flags to `appsettings.json`:
  - `UseWolverineEndpoints`, `UseMartenReads`, `UseCommandQueue`, `UseSagas`, `UseSignalR`, `UseOutbox`
- [x] Read flags in `Program.cs` and conditionally register services
- [x] Create migration checklist: provision Postgres, run schema migration, enable flags in order, verify each step
- [x] Parity verification: run tests with all flags OFF (identical to pre-Wolverine), then ON one by one, then all ON
- [ ] After verification, remove conditional code: delete `if (featureFlag)` branches, remove old read/write paths

## Key decisions

- **Feature flags are temporary.** After Phase 5, remove them.
- **Rollback strategy:** disable any problematic flag in production

## Files

- `Genelife.API/appsettings.json`
- `Genelife.API/Program.cs`
- `Genelife.API/Services/SimulationManager.cs`
- new `docs/plans/hybrid-wolverine/migration-checklist.md`

## Acceptance

- Toggling individual flags changes behavior correctly
- All flags OFF: identical to pre-Wolverine behavior
- All flags ON: full Wolverine architecture

# H20 — Feature flags and parity verification

**Phase:** 5 — Realtime and Cleanup
**Depends:** H19
**Estimate:** 1 session

**Status:** Done — feature flags and conditional registration removed; parity tests guard the single path.

## Goal

Add feature flags to toggle between old and new architecture paths. Create a migration checklist. After verification, remove old code paths entirely.

## Tasks

- [x] Add feature flags to `appsettings.json`:
  - `UseWolverineEndpoints`, `UseMartenReads`, `UseCommandQueue`, `UseSagas`, `UseSignalR`, `UseOutbox`
- [x] Read flags in `Program.cs` and conditionally register services
- [x] Create migration checklist: provision Postgres, run schema migration, enable flags in order, verify each step
- [x] Parity verification: run tests with all flags OFF (identical to pre-Wolverine), then ON one by one, then all ON
- [x] After verification, remove conditional code: delete `if (featureFlag)` branches, remove old read/write paths

## Key decisions

- **Feature flags are temporary.** After Phase 5, remove them.
- **Rollback strategy:** flags are gone; roll back by deploying the previous build and restoring the pre-migration snapshot.

## Files

- `Genelife.API/appsettings.json`
- `Genelife.API/Program.cs`
- `Genelife.API/Services/SimulationManager.cs`
- new `docs/plans/hybrid-wolverine/migration-checklist.md`

## Resolution

`UseWolverineEndpoints`, `UseMartenReads`, `UseCommandQueue`, `UseSagas`, `UseSignalR`
and `UseOutbox` were inventory: only the first two were read, and both were always
true in every deployed configuration. The conditionals were removed, Marten and
Wolverine are unconditional, and the dead config entries were deleted. The
`EndpointParityTests` suite asserts the remaining routes, status codes and policies,
and fails if a legacy endpoint type reappears.

## Acceptance

- No `FeatureFlags` section or conditional registration remains
- All routes return their predecessor's status codes via Wolverine
- Full PostgreSQL suite passes on the single path

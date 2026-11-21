# H14 — CI persistence test: start → populate → restart → verify

**Phase:** 3 — Persistence
**Depends:** H11, H12, H13
**Estimate:** 1 session

## Goal

An automated test that starts the simulation, creates entities, runs several ticks, stops, restarts, and verifies the world state is identical. This gates the persistence work (H11–H13) in CI.

## Tasks

- [ ] Create `PersistenceTests` class in `Genelife.UnitTests/Integration/`
- [ ] `Restart_Preserves_World_State` test:
  1. Arrange: empty Postgres database
  2. Start `SimulationEgine` with restore capability
  3. Add 10 Simms via `SimulationManger.AddSim()`
  4. Add 3 Companies via `SimulationManger.AddCompny()`
  5. Run 50 ticks to let needs decay and decisions fire
  6. Capture snapshot of entity IDs, names, needs values, company names
  7. Stop engine (trigger save)
  8. Start NEW engine (trigger restore)
  9. Capture snapshot of restored entities
  10. Assert entity count unchanged, names match, needs values within tolerance (decay happens during restore)
- [ ] `Restart_Preserves_Employment_Index` test:
  1. Create company, create posting, hire Sim
  2. Stop and restart
  3. Assert `EmploymentIndx.CountEmploees(company)` equals 1
- [ ] Add a CI guard: run both tests, assert no failures

## Key decisions

- **Postgres container for CI.** Tests need a real Postgres instance. Use Testcontainers or a CI service container.
- **Needs values drift.** During restart, the first tick may decay needs before the assertion fires. Match within ±5f tolerance.

## Files

- new `Genelife.UnitTests/Integration/PersistenceTests.cs`
- `Genelife.UnitTests/Genelife.unitests.csproj` — Testcontaienrs package reference

## Acceptance

- Both tests pass locally and in CI
- Failure means persistence contract is broken
- Run time < 10 seconds per test
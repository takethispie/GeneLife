# L19 — Persistence and restore

**Phase:** 8 — Durability
**Depends:** L01–L18, E19, H11
**Estimate:** 3 sessions
**Status:** Implemented (V1)

## Goal

Persist the full lifecycle and rebuild it exactly on restart, including stage-specific components, schools, pensions, health and pending deaths.

## Context

Snapshots currently store name, age, needs, action, wallet and a job-seeking flag. Restoring would erase stages, skills, enrollment, employment records, health and pensions. Age jumps across stages must also be handled.

## Design

### Snapshot additions

`PersistedSim` gains:

- `Stage`, `AgeTicks`, `StageEnteredTick`, `LastCause`;
- `Skills` (six floats), `SkillMask`;
- `Health`, `DeclineRate`, `ChronicConditions`;
- `SchoolNetworkId`, `EnrollmentMonths`, `Attendance`;
- `Seniority`, `EmployerNetworkId`, `OfficeNetworkId`, `SalaryCents`, `HiredAtTick`;
- `EmploymentRecord` fields;
- `PensionAccount` fields;
- `RetirementIncome` fields;
- `UnemploymentRecord` fields;
- `IsDead`, `DeathTick`, `DeathCause`.

New sections:

- `Schools`, `SchoolEnrollments`;
- `MortalityLog`;
- `ImmigrationState` (budget, pending arrivals, paused flag);
- `GenerationSeed`.

### Restore order

1. Government, pension fund, market state.
2. Companies, offices, postings, seats.
3. Schools.
4. Properties, units, households.
5. Sims with lifecycle, skills, health and money.
6. Employment relations and `EmploymentIndex`.
7. Enrollment and school counts.
8. Pension accounts, retirement income and fund reconciliation.
9. Unemployment and benefit records.
10. Mortality queue and dead markers.
11. Immigration pending pool.
12. Tick and random seeds.
13. Rebuild `HousingRegistry`, `LifecycleIndex` and `Residence`.
14. Run invariant validator and fail restore on violation.

### Age jump replay

If a saved sim has `AgeTicks` beyond its saved stage boundary, restore applies the missing transitions in order before the world starts. This covers long downtime or edited saves. Every applied transition emits a `LifeStageChanged` with cause `Restored` and is logged.

### Marten documents

- Extend `SimDocument` with stage, age ticks, skills, health, employer/housing ids, pension and retirement fields.
- New `SchoolDocument`, `EnrollmentDocument`, `MortalityDocument`, `ImmigrationDocument`.
- Projections remain inline for entity documents; mortality log uses the async writer.

### Migration

- v1/v2 sims without lifecycle are classified by `Alive.Age`.
- Students get an empty profile and enrollment attempt.
- Workers get a profile derived from their job, or a default.
- Retirees get a pension account consistent with age.
- Elders get health consistent with age.

## Tasks

- [ ] Add all persisted fields and sections; bump schema.
- [ ] Extend capture/restore and document models.
- [ ] Implement index rebuilds and the invariant validator.
- [ ] Implement age-jump replay.
- [ ] Implement the legacy migration path.
- [ ] Add round-trip tests for every stage.
- [ ] Add a corruption test (pension fund mismatch, student with employee).
- [ ] Add a 10-year save/restore/continue scenario.

## Files

- `Genelife.API/Persistence/PersistedWorldState.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.API/Models/SimDocument.cs`, new documents
- `Genelife.API/Projections/*`
- `Genelife.API/Services/WorldSaveService.cs`, `WorldRestoreService.cs`
- `Genelife.UnitTests/Integration/PersistenceTests.cs`

## Acceptance

- Every stage round-trips with all components intact.
- Rebuilt indices match recomputed values.
- Age-jump replay applies all intermediate transitions once.
- Pension fund reconciles with statements after restore.
- Legacy snapshots migrate without losing sims.
- Corrupt state is rejected with a specific error.
- A 10-year run survives save/restore and continues with identical invariants.

## Testing

- Stage round-trip matrix.
- Index rebuild equality.
- Age-jump replay test.
- Migration fixture test.
- Corruption rejection.
- Continuation comparison test.

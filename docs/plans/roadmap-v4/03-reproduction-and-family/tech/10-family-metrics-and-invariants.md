# F10 — Family metrics and invariants

**Phase:** 10 — Observability
**Depends:** F01–F09
**Estimate:** 1–2 sessions
**Status:** Planned (V4)

## Goal

Make birth, pregnancy, childhood and guardian state visible and catch incomplete newborns, missing guardians and child work before they corrupt continuity.

## Context

The lifecycle metrics already cover population stages, deaths and immigration. V4 adds births and family state; those need their own counters and invariants.

## Design

### Metrics

`ReproductionMetricsSystem` at order 622, yearly:

- pregnancies started, completed and lost;
- births this year;
- birth rate per 1,000 sims;
- average age at birth;
- children (0–5 and 6–17);
- average household size;
- overcrowded units;
- children without a guardian;
- children enrolled and not enrolled;
- child costs paid and unmet;
- child benefit paid (if enabled).

### Invariants

| Check | Level |
|---|---|
| Active pregnancy has a valid carrying parent and other parent | Fail |
| At most one active pregnancy per carrying parent | Fail |
| `DueTick > ConceivedTick` and `ChildrenExpected >= 1` | Fail |
| Newborn has `Alive`, `Lifecycle`, `Traits`, `GenName`, `HouseholdMember`, `Residence` (when a unit exists) | Fail |
| Newborn has two parent edges, or one if a parent is unavailable | Fail |
| Minor has a guardian or the household head fallback | Fail |
| Child is not employed or receiving a pension | Fail |
| Child under `SchoolStartAge` is not enrolled | Warn |
| Child 6–17 is enrolled or has a documented exclusion | Warn |
| Unit over capacity | Warn |
| Guardian household is not the child's residence | Warn |

### Read model

- `FamilyStatus` includes the yearly reproduction snapshot.
- `/api/family/health` includes the invariants.
- The admin page shows a badge and the failing rows.

### Determinism

- The yearly metrics are derived from state, not from event order.
- Replaying the same seed produces the same counts.
- A zero-birth year is a warning for the balance review, not a silent pass.

## Tasks

- [ ] Add `ReproductionMetricsSystem` and the yearly snapshot.
- [ ] Add `ReproductionInvariants`.
- [ ] Add health checks to the family status.
- [ ] Add the admin badge and detail list.
- [ ] Add metric and invariant tests.
- [ ] Add a replay comparison test.

## Files

- new `Genelife/Systems/ReproductionMetricsSystem.cs`
- new `Genelife/Family/ReproductionInvariants.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.Contracts/Dtos/FamilyStatus.cs`
- `Genelife.UnitTests/Family/ReproductionMetricsTests.cs`

## Acceptance

- Every invariant has a deliberate-corruption test.
- Yearly metrics are captured with one population pass.
- The admin health endpoint matches the invariant test.
- Replay produces the same metrics.
- A newborn missing any required component is a visible failure.

## Testing

- Remove a parent edge and assert a failure.
- Give a child a job and assert a failure.
- Create an overcrowded unit and assert a warning.
- Replay a seeded decade and compare counts.
- Assert the page health badge reflects the failure.
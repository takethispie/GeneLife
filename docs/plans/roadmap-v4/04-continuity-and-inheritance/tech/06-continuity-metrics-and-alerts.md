# C06 — Continuity metrics and alerts

**Phase:** 6 — Observability
**Depends:** C01–C05
**Estimate:** 1–2 sessions
**Status:** Partial — snapshot fields, invariants and health done; alerts/UI pending

## Goal

Expose births, natural growth, replacement, orphans, trusts and inheritance so a continuity defect is visible before a decade run fails.

## Context

`PopulationSnapshot` currently covers stages, deaths, arrivals, health, pensions and dependency. V4 adds births, family households, guardianship and trusts.

## Design

### Snapshot fields

Add to `PopulationSnapshot` and the persisted snapshot:

- `BirthsThisYear`;
- `BirthRate` per 1,000;
- `NaturalChange`;
- `NetMigration`;
- `ReplacementRate`;
- `Children`;
- `ChildrenUnderSchoolAge`;
- `AverageHouseholdSize`;
- `Orphans`;
- `StateCareChildren`;
- `GuardianCoverage`;
- `MinorTrustTotal`;
- `InheritancePaidThisYear`;
- `TrustReleasedThisYear`.

### Invariants

`ContinuityInvariants.Verify`:

| Check | Level |
|---|---|
| Living minor has a guardian edge or state-care record | Fail |
| Minor trust beneficiary is the child and the guardian is valid | Fail |
| Trust balance is included in total money supply | Fail |
| Births counted once and the pregnancy is completed | Fail |
| Death ends the active partnership and reassigns the head | Fail |
| Heir shares sum to the distributable estate | Fail |
| Population target is a floor, not a cap | Warn |
| Orphan count is nonzero and no state-care record exists | Fail |
| Child costs are missing for a dependent child | Warn |
| Projection is stale | Warn |

### Read model and alerts

- `LifecycleStatus` and `FamilyStatus` include the continuity block.
- `/api/lifecycle/health` and `/api/family/health` expose the checks.
- The admin pages show a badge and the failing rows.
- A yearly alert is emitted when births are zero for a configured window or the replacement rate leaves the band.

### Determinism

- Metrics are derived from state, not event order.
- Replay produces the same counts.
- A missing birth record is a failure, not a silent zero.

## Tasks

- [ ] Add the snapshot fields and persistence.
- [ ] Add `ContinuityInvariants`.
- [ ] Add the health checks and alerts.
- [ ] Extend the lifecycle/family read models.
- [ ] Add the admin badges.
- [ ] Add metric and invariant tests.
- [ ] Add a replay comparison.

## Files

- `Genelife/Life/PopulationSnapshot.cs`
- new `Genelife/Family/ContinuityInvariants.cs`
- `Genelife/Systems/PopulationMetricsSystem.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.Contracts/Dtos/LifecycleStatus.cs`
- `Genelife.UnitTests/Family/ContinuityMetricsTests.cs`

## Acceptance

- Births, natural change and replacement are visible.
- Every invariant has a deliberate-corruption test.
- The health endpoints match the invariant results.
- Admin pages show continuity alerts.
- Replay produces the same metrics.

## Testing

- Remove a guardian edge and assert a failure.
- Create a trust with a missing beneficiary and assert a failure.
- Set births to zero and assert the alert window.
- Replay a decade and compare metrics.
- Assert the trust total is included in money supply.
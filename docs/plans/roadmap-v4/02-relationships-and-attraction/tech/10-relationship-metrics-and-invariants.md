# R10 — Relationship metrics and invariants

**Phase:** 10 — Observability
**Depends:** R01–R09
**Estimate:** 1 session
**Status:** Partial — invariants done; yearly metrics history pending

## Goal

Catch broken edges, duplicate partners, impossible ancestry and missing parents before they corrupt inheritance or reproduction.

## Context

The relationship graph is now a dependency of inheritance, guardianship, housing and the admin. A mirror edge that exists in one direction but not the other is a silent data defect until an heir is missed.

## Design

### Invariants

`RelationshipInvariants.Verify(world, index)`:

| Check | Level |
|---|---|
| Edge endpoint is alive or explicitly historical | Fail if a living operation references a destroyed sim |
| Mirror edge exists and points back | Fail |
| No duplicate `(From, To, Kind)` | Fail |
| No self-edge | Fail |
| Partner mirror is mutual and active | Fail |
| At most one active partner | Fail |
| No ancestry cycle | Fail |
| Index lists equal component counts | Fail |
| Parent edge does not point to a dependant without a guardian | Warn |
| Partner households split for more than N months | Warn |
| Projection is stale | Warn |

### Metrics

`FamilyMetricsSystem` at order 621, yearly:

- living sims, eligible singles, partnered sims;
- partnership rate by age band;
- average and p95 age gap;
- attraction score p50/p95;
- average, p95 and max candidate count;
- pairing rejections this year;
- household merges and splits this year;
- parents with children in a different household;
- index rebuild time.

### Read model

- `FamilyStatus` includes the yearly snapshot and current health.
- `/api/family/health` returns the invariants and projection staleness.
- The admin page shows a badge when any invariant fails.

### Determinism checks

- Record the count of edges created per year.
- Replay the same seed and compare counts and score distribution.
- A sudden zero-partnership year is a warning, not silently accepted.

## Tasks

- [ ] Add `RelationshipInvariants`.
- [ ] Add `FamilyMetricsSystem` and the yearly snapshot.
- [ ] Add score and candidate distributions.
- [ ] Add health checks to `/api/family/health`.
- [ ] Add the admin badge and detail list.
- [ ] Add invariant and metric tests.

## Files

- new `Genelife/Family/RelationshipInvariants.cs`
- new `Genelife/Systems/FamilyMetricsSystem.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.Contracts/Dtos/FamilyStatus.cs`
- `Genelife.UnitTests/Family/RelationshipMetricsTests.cs`

## Acceptance

- Every invariant has a test that deliberately corrupts state and asserts the failure.
- Yearly metrics are captured without scanning the population more than once.
- The admin health endpoint reflects the same issues as the invariant test.
- A deterministic replay produces the same metric values.

## Testing

- Remove a mirror edge and assert a failure.
- Add a second partner and assert a failure.
- Create a two-node ancestry cycle and assert a failure.
- Rebuild the index and compare counts.
- Replay a seeded world and compare metrics.
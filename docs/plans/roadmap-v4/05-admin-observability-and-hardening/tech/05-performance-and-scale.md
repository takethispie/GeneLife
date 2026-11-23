# H05 — Performance and scale

**Phase:** 5 — Verification
**Depends:** features 01–04
**Estimate:** 2 sessions
**Status:** Planned (V4)

## Goal

Keep the family systems inside the existing tick budget at 2,000 sims and prevent an accidental all-pairs or full-scan regression.

## Context

The V2 map and V1 lifecycle already have performance guardrails. Relationships, pregnancy and family reconciliation add monthly and quarterly work. Candidate generation is the highest-risk path.

## Design

### Budgets

| Path | Budget |
|---|---|
| Relationship exposure (monthly) | bounded by candidate cap; no full population pair scan |
| Attraction (quarterly) | ≤ 24 candidates per eligible sim |
| Pairing (quarterly) | O(eligible sims × log candidates) |
| Pregnancy (monthly) | O(active pregnancies) |
| Birth (monthly) | O(due pregnancies) |
| Family household (monthly) | O(pending merges + households needing reconciliation) |
| Family metrics (yearly) | one population pass |
| Read model | paginated; no unbounded list |

Targets are measured against the existing tick p95 at 2,000 sims, not a synthetic microbenchmark alone.

### Instrumentation

- `FamilyMetricsSystem` records candidate count average/p95/max.
- `SimulationEngine` records family system time per tick.
- The admin status exposes the last tick budget and the worst system.
- A regression test fails if the candidate pass grows super-linearly with population.

### Data volume

- Relationships: O(sims) edges.
- Pregnancies: O(active parents) plus history.
- Trusts: O(minor heirs).
- Projections: one document per sim/edge/pregnancy/child; paginated reads.
- Snapshot: eight trait ints + relationship/pregnancy/trust lists per sim; measure at 2,000.

### Scale scenarios

1. 2,000 sims, 10 years, births enabled.
2. 5,000 sims, 1 year, relationships only (smoke).
3. 2,000 sims, 200 relationships per sim worst case (cap test).
4. Restore a 2,000-sim save and continue 1,000 ticks.

### Failure policy

- If a pass exceeds its budget, reduce the cadence or candidate cap before increasing the tick time.
- Do not silently skip eligible sims; record the skipped count.
- Do not move pairing onto a background thread that mutates the world.

## Tasks

- [ ] Add family timing/candidate instrumentation.
- [ ] Add the 2,000-sim decade scenario.
- [ ] Add the 5,000-sim smoke.
- [ ] Add the cap/worst-case test.
- [ ] Add the restore-and-continue timing test.
- [ ] Add the super-linearity guard.
- [ ] Record the numbers in `STATUS.md`.

## Files

- `Genelife/Systems/FamilyMetricsSystem.cs`
- `Genelife/Systems/AttractionSystem.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.UnitTests/Performance/FamilyPerformanceTests.cs`
- `docs/plans/roadmap-v4/STATUS.md`

## Acceptance

- The 2,000-sim decade stays inside the existing tick budget.
- Candidate count never exceeds the cap.
- The super-linearity guard passes.
- A restore-and-continue run does not grow without bound.
- No family system performs a full population cross-product.
- The timing numbers are recorded.

## Testing

- 2,000-sim decade.
- 5,000-sim smoke.
- Worst-case candidate cap.
- Restore-and-continue.
- Allocation/tick p95 comparison with the pre-V4 baseline.
# H07 — Scenario and acceptance tests

**Phase:** 7 — Verification
**Depends:** features 01–05
**Estimate:** 2 sessions
**Status:** Planned (V4)

## Goal

Define the release-gating scenarios that prove V4 works as a system, not only as a set of passing unit tests.

## Context

Each feature has its own acceptance doc. This step assembles the cross-feature scenarios, assigns them to test categories and defines the report that goes into `STATUS.md`.

## Design

### Scenario matrix

| Scenario | Population | Years | Gates |
|---|---|---|---|
| Trait seeds | 200 | 0 | all living sims have traits; distribution plausible |
| Family decade | 200 | 10 | partnerships, births, school, work, household formation |
| Generation | 200 | 30 | replacement, inheritance, dependency, no collapse |
| Scale | 2,000 | 10 | tick budget, candidate cap, snapshot size |
| Migration | legacy fixture | 1 | schema 5 → 6, no invented parents, exact restore |
| Replay | 200 | 10 | identical traits/graph/births/money |
| Conservation | 200 | 10 | money gap 0, trust gap 0 |
| Admin | 200 | 1 | page, endpoints, operations, authorization |
| Error injection | small | 1 | projection failure, dangling edge, invalid enum |

### Test categories

- `Family.Traits`
- `Family.Relationships`
- `Family.Reproduction`
- `Family.Continuity`
- `Family.Persistence`
- `Family.Replay`
- `Family.Performance`
- `Family.Admin`

Every category has a fast unit suite and at least one integration scenario.

### Release report

Write a markdown report to `docs/plans/roadmap-v4/STATUS.md` with:

- scenario name, seed, population, years;
- births, deaths, arrivals, replacement rate;
- partnership rate, average age gap;
- orphan/state-care counts;
- conservation and trust gaps;
- tick p95, candidate p95, snapshot size;
- known failures and target misses;
- date and build commit.

### CI gating

- Fast family suites run on every change.
- The decade and 2,000-sim scenarios run before release.
- The migration and replay suites run before release.
- A performance regression fails the release job.

### Failure policy

- A failed scenario is a release blocker unless documented as a known gap.
- Do not lower a target to make a scenario pass without an explicit decision in the balance doc.
- A flaky test is fixed or quarantined with an issue, not silently retried.

## Tasks

- [ ] Add the scenario matrix and categories.
- [ ] Add the release report generator or template.
- [ ] Wire the fast and release CI jobs.
- [ ] Add the error-injection suite.
- [ ] Add the report output to `STATUS.md`.
- [ ] Define the flaky-test policy.
- [ ] Record the first full run.

## Files

- `Genelife.UnitTests/Family/*`
- `Genelife.UnitTests/Persistence/*`
- `Genelife.UnitTests/Performance/FamilyPerformanceTests.cs`
- `docs/plans/roadmap-v4/STATUS.md`
- CI workflow for the family release job

## Acceptance

- Every scenario in the matrix exists and is runnable.
- The release report is generated and checked in.
- Fast suites gate normal changes; slow suites gate release.
- A performance regression fails the release job.
- A documented known gap does not silently pass as green.

## Testing

- Run the full matrix on a clean build.
- Run the fast suites twice to check flakiness.
- Run the release report and compare with the previous run.
- Simulate a regression and assert the gate fails.
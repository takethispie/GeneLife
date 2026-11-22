# H14 — Scenario tests

**Phase:** 11 — Verification
**Depends:** all housing steps
**Estimate:** 2 sessions
**Status:** Planned

## Goal

Prove housing survives shocks and remains legible over a decade, not just in a single happy-path rent cycle.

## Scenarios

### S1 — Steady state

1,000 households, 1,100 units, stable jobs. Run 10 years.

Assertions:

- vacancy 3–8%;
- homelessness under 3%;
- rent arrears under 10% of rent roll;
- no unit with two households;
- rent index inside bounds;
- median rent burden 25–40%.

### S2 — Company collapse

Bankrupt the largest employer at year 2.

Assertions:

- unemployment rises then recovers via new postings/immigration;
- evictions rise but do not exceed the per-cycle cap;
- shelter occupancy rises and triggers municipality construction;
- no mass simultaneous eviction.

### S3 — Rent shock

Raise the market index to its cap for two years.

Assertions:

- households move down-market;
- arrears rise but remain under fail thresholds;
- construction responds within 12 months;
- index mean-reverts after vacancy rises.

### S4 — Housing shortage

Remove 20% of units.

Assertions:

- vacancy falls below 2% and warns;
- overcrowding rises;
- homelessness rises but shelter capacity and construction respond;
- population growth slows through immigration.

### S5 — Immigration wave

Add 300 working-age households in one year.

Assertions:

- assignment fills vacancies before shelter;
- construction starts within a year;
- rent index rises within bounds;
- no household is assigned above capacity.

## Harness

- `HousingScenarioHarness` builds a deterministic world from a seed and a scenario id.
- Runs the real `SystemRegistry`, not a parallel simulation.
- Captures yearly `HousingSnapshot` and `EconomySnapshot`.
- Writes a JSON artifact to `performance-baselines/housing/`.
- Fails with a metric diff on the first violated assertion.

## Tasks

- [ ] Add the harness and scenario definitions.
- [ ] Add assertion helpers with readable diffs.
- [ ] Store expected bands in one place shared with H13.
- [ ] Add artifact output and CI comparison.
- [ ] Add regression fixtures for the current known-good seed.
- [ ] Run scenarios at 1,000 and 2,000 households for performance.
- [ ] Document how to reproduce each scenario in the plan README.

## Files

- new `Genelife.UnitTests/Housing/HousingScenarioHarness.cs`
- new `Genelife.UnitTests/Housing/HousingScenarioTests.cs`
- `performance-baselines/housing/*.json`

## Acceptance

- All five scenarios pass on the reference seed.
- A deliberately broken policy fails at least one assertion with a clear message.
- Scenarios are deterministic across runs and machines.
- Full housing suite runs in under five minutes locally.

## Testing

The scenarios are the tests. This step is accepted when they are part of CI and the baseline artifact is checked in.

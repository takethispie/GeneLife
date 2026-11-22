# L22 — Testing and acceptance

**Phase:** 10 — Verification
**Depends:** all life-stage steps
**Estimate:** 2–3 sessions
**Status:** Implemented (V1)

## Goal

Prove the full lifecycle works as a system: transitions, careers, pensions, health, death, estates and population stability.

## Test layers

### Unit

- stage classification and policy validation;
- aging accumulation and birthday offsets;
- transition handlers and idempotency;
- enrollment ranking and attendance sensitivity;
- skill gain, caps and bitmask derivation;
- graduation salary formula;
- shift lifecycle and interruption thresholds;
- attendance/performance formulas;
- promotion scores and salary bands;
- unemployment eligibility, benefit duration and salary decay;
- retirement conversion;
- pension accrual, yield and payout waterfall;
- health drift and elderly scaling;
- mortality risk distribution;
- estate priority and distribution;
- inflow triggers and budget;
- generator determinism.

### Integration

- Student enrolls → learns → graduates → applies → hired → works → promoted → retires → receives pension → declines → dies → estate settled.
- Worker resigns after arrears → benefit → salary decay → rehired.
- Retiree outlives the pension fund → government top-up → reduced pension.
- Elder with poor housing and low health dies earlier than one in good housing.
- Death of a household head → tenancy continues → estate distributes.
- Last household member dies → unit released → new household moves in.
- Population crash → inflow → recovery.

### Scenario

Deterministic 10-year run with 1,000 sims:

- stage shares within target bands;
- enrollment ≥ 90%;
- no student employed, no retiree job seeking;
- pension fund reconciles and funding ratio above fail threshold;
- population change within ±5%;
- life expectancy 70–85;
- every death has one settlement;
- every estate conserves money;
- no entity leaks (sims, jobs, units, school slots).

### Performance

- 2,000 sims, all systems registered;
- p95 tick within the existing budget;
- aging and stage progression allocate below their ceilings;
- no mortality or estate work on every tick beyond the queues.

## Tasks

- [ ] Add the unit suites listed above.
- [ ] Add the full-life integration test.
- [ ] Add the 10-year population scenario harness.
- [ ] Add the entity-leak and invariant checks.
- [ ] Add performance guardrails for aging, health and stage progression.
- [ ] Store baseline artifacts for comparison.
- [ ] Document reproduction from a seed and config.
- [ ] Gate merge on build, tests and the population guardrail.

## Files

- `Genelife.UnitTests/Systems/*`
- new `Genelife.UnitTests/Lifecycle/FullLifecycleTests.cs`
- new `Genelife.UnitTests/Lifecycle/PopulationScenarioTests.cs`
- new `Genelife.UnitTests/Lifecycle/InvariantTests.cs`
- `Genelife.UnitTests/Performance/PerformanceGuardrailTests.cs`
- `performance-baselines/lifecycle/`

## Acceptance

- All tests pass in CI.
- The full lifecycle test completes within two minutes.
- The 10-year scenario meets the bands or fails with a metric diff.
- No flaky test depends on wall clock or unseeded randomness.
- Entity, job, housing and school slot counts return to expected values after deaths and restarts.

## Acceptance gate

Life stages are done when a generated world can run for ten simulated years without intervention, keep a plausible age pyramid, pay funded pensions, settle every death, and remain fully observable and operable in the admin UI.

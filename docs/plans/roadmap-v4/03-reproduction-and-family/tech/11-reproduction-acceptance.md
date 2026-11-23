# F11 — Reproduction acceptance

**Phase:** 11 — Verification
**Depends:** F01–F10
**Estimate:** 1–2 sessions
**Status:** Planned (V4)

## Goal

Prove conception, pregnancy, birth, childhood and child costs are deterministic, complete and safe before continuity and inheritance depend on them.

## Acceptance scenarios

### Conception

1. Eligible partners conceive at the configured rate over 1,000 pair-months.
2. Minors, dependants, the dead, the already-pregnant and the unhealthy never conceive.
3. A loss starts a cooldown.
4. Replay the same seed and compare the pregnancy set.

### Pregnancy

1. A pregnancy progresses monthly and becomes ready at `DueTick`.
2. Prenatal care is charged and ledgered.
3. Insufficient funds produce arrears and higher risk, not negative money.
4. A loss sets status, emits an event and preserves history.

### Birth

1. A newborn has traits, parent edges, guardian household, residence, education stage and dependent tenure.
2. Traits are inherited and the name is deterministic.
3. A full unit becomes overcrowded and the birth still completes.
4. The pregnancy is `Completed` and the birth is recorded.
5. No money is minted.

### Childhood

1. A newborn is not enrolled at age 0.
2. The child enrolls at `SchoolStartAge`.
3. The child is never employed or pensioned.
4. At `WorkStartAge` the sim becomes a job seeker and clears dependent tenure.
5. Parent edges survive the transition.

### Household

1. A birth into a full unit is overcrowded and visible.
2. A merge retries when capacity appears.
3. An adult child moves out only under policy.
4. A guardian change moves the child.
5. `HousingRegistry.Verify` passes after each scenario.

### Child costs

1. The guardian pays for child consumption.
2. Unpaid costs become arrears/unmet demand.
3. School and health costs keep their existing destinations.
4. Optional child benefit is a ledgered transfer.
5. Conservation holds.

### Admin

- Pregnancies and children are visible with parents, guardian, household and school.
- Force/cancel pregnancy and assign guardian return `202`.
- Non-admin mutations are rejected.

### Restore and performance

1. Save during pregnancy and after birth; restore; assert exact state.
2. Continue 1,000 ticks without divergence.
3. 2,000-sim decade stays inside the tick budget.
4. The balance scenario remains green with births enabled.

## Tasks

- [ ] Add the reproduction acceptance suite under `Genelife.UnitTests/Family`.
- [ ] Add the conception replay fixture.
- [ ] Add the full pregnancy/birth scenario.
- [ ] Add the childhood age-gate scenario.
- [ ] Add the household capacity/guardian scenario.
- [ ] Add the child-cost conservation test.
- [ ] Add save/restore during pregnancy.
- [ ] Add the 2,000-sim performance scenario.
- [ ] Record the result in `roadmap-v4/STATUS.md`.

## Files

- `Genelife.UnitTests/Family/ReproductionAcceptanceTests.cs`
- `Genelife.UnitTests/Family/BirthTests.cs`
- `Genelife.UnitTests/Family/ChildhoodTests.cs`
- `Genelife.UnitTests/Persistence/PregnancyRestoreTests.cs`
- `docs/plans/roadmap-v4/STATUS.md`

## Acceptance

- All scenarios pass on a clean build.
- No newborn is missing a required component or edge.
- No child is employed or enrolled too early.
- A birth never fails for capacity and never mints money.
- Restore during pregnancy is exact.
- The 10-year balance scenario and existing suites remain green.

## Testing

- Full family suite.
- Persistence, snapshot and integration suites.
- 2,000-sim pregnancy/birth timing.
- A 10-year balance report with births and immigration both enabled.
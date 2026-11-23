# C07 — Continuity acceptance

**Phase:** 7 — Verification
**Depends:** C01–C06
**Estimate:** 2 sessions
**Status:** Planned (V4)

## Goal

Prove births, immigration, death, guardianship, inheritance and trusts form a stable, conserving population loop.

## Acceptance scenarios

### Natural growth

1. Generate 200 sims with partnerships and a conception rate.
2. Run 10 years.
3. Assert births > 0, children exist, school enrollment and work transitions occur.
4. Assert the population does not collapse.
5. Assert conservation and trust gaps are zero.

### Replacement over a generation

1. Run 30 years at 200 sims.
2. Assert the replacement rate is within the configured band.
3. Assert the age pyramid has children, workers and retirees.
4. Assert no orphan is without a guardian or state care.
5. Assert the pension funding ratio does not collapse.

### Immigration interaction

1. Births close the target gap and immigration pauses with `TargetReached`.
2. Deaths reopen the gap and immigration resumes.
3. A baby boom above target does not trigger a correction.
4. Pending arrivals count against the gap.

### Death and inheritance

1. A partner dies: partnership ends, head/guardian reassigned, parent edges remain.
2. Children inherit through the relationship order.
3. A minor's share enters a trust and is released at adulthood.
4. Owned housing transfers to the partner or guardian.
5. No money is minted or destroyed.

### Orphans

1. Both parents die: relative guardian.
2. No relative: state care with government cost.
3. Guardian dies: next guardian or state care.
4. No child is silently dropped.

### Restore

1. Save with active pregnancies, trusts and guardianships.
2. Restore.
3. Assert exact state and rebuilt indices.
4. Continue 1,000 ticks without divergence.

### Admin

- The lifecycle page shows births, replacement, orphans and trust totals.
- The family page shows guardians and state care.
- An inheritance transfer is visible with the heir order.

### Performance and balance

- 2,000-sim family decade stays inside the tick budget.
- The existing 10-year balance scenario remains green.
- The family scenario report is recorded in the balance-target doc.

## Tasks

- [ ] Add the continuity acceptance suite.
- [ ] Add the 10-year and 30-year scenarios.
- [ ] Add the immigration/birth interaction tests.
- [ ] Add the inheritance/trust/orphan scenarios.
- [ ] Add save/restore with active family state.
- [ ] Add the 2,000-sim performance run.
- [ ] Record the results in `roadmap-v4/STATUS.md`.
- [ ] Update the balance-target doc with the family report.

## Files

- `Genelife.UnitTests/Family/ContinuityAcceptanceTests.cs`
- `Genelife.UnitTests/Family/FamilyScenarioTests.cs`
- `Genelife.UnitTests/Persistence/FamilyRestoreTests.cs`
- `docs/plans/roadmap-v4/STATUS.md`
- `docs/plans/roadmap-v1/01-economy/business/04-balance-targets.md`

## Acceptance

- All scenarios pass on a clean build.
- Births are a visible continuity path.
- No orphan is unassigned.
- Inheritance and trusts conserve money.
- Restore is exact.
- The balance scenario and existing suites remain green.

## Testing

- Full family/continuity suite.
- Persistence, snapshot and integration suites.
- 10-year and 30-year scenario runs.
- 2,000-sim performance run.
- Replay comparison.
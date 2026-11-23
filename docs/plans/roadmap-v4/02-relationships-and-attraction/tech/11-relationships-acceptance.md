# R11 — Relationships acceptance

**Phase:** 11 — Verification
**Depends:** R01–R10
**Estimate:** 1–2 sessions
**Status:** Done

## Goal

Prove the family graph, eligibility, attraction, pairing and household merge are deterministic, consistent and safe before reproduction depends on them.

## Acceptance scenarios

### Seeded family

1. Generate 200 sims with a known seed.
2. Assert a configured share of adults has partner edges.
3. Assert every seeded child has at least one parent edge and a guardian household.
4. Assert no minor has a partner.
5. Assert no close relative has a partner edge.

### Eligibility matrix

- minor/adult;
- existing partner;
- parent/child;
- siblings;
- grandparent/grandchild;
- aunt/uncle;
- first cousins;
- age gap over the limit;
- same-sex allowed/denied by policy.

Each case must return the configured reason and no score.

### Attraction

1. Pin a known pair's score and components.
2. Assert bounds and symmetry.
3. Change one trait and assert the expected direction.
4. Assert an ineligible pair scores 0.

### Pairing

1. Mutually top-scored pair forms a partnership.
2. One-sided top pair does not form unless the greedy rule matches.
3. A rejection creates a cooldown and retries after expiry.
4. No duplicate or second partner edge.
5. Replay the same seed and compare the partnership set.

### Household merge

1. Merge when capacity allows.
2. Do not merge when capacity is short.
3. Preserve the target head.
4. Update member and occupant counts and `Verify` the registry.
5. Split a household and preserve dependants.
6. Assert no money is minted by the merge.

### Restore and index

1. Save after pairing and merging.
2. Restore.
3. Assert the relationship index rebuilds to the same lookups.
4. Continue 1,000 ticks and assert no mirror or duplicate defect.

### Admin

- The family page shows a seeded family.
- An attraction explanation lists components.
- Force/end partnership works after the next tick.
- Non-admin mutation is rejected.

### Performance

- 2,000 sims, 10-year run.
- Candidate pass stays under the configured cap and quarterly budget.
- The full decade balance scenario remains green.

## Tasks

- [ ] Add the relationship acceptance suite under `Genelife.UnitTests/Family`.
- [ ] Add the deterministic seed-family fixture.
- [ ] Add the eligibility matrix.
- [ ] Add the pairing replay test.
- [ ] Add the household merge/split and registry verify test.
- [ ] Add save/restore/index test.
- [ ] Add the 2,000-sim performance scenario.
- [ ] Record the result in `roadmap-v4/STATUS.md`.

## Files

- `Genelife.UnitTests/Family/RelationshipAcceptanceTests.cs`
- `Genelife.UnitTests/Family/EligibilityTests.cs`
- `Genelife.UnitTests/Family/PairingSystemTests.cs`
- `Genelife.UnitTests/Family/HouseholdMergeTests.cs`
- `Genelife.UnitTests/Persistence/RelationshipRestoreTests.cs`
- `docs/plans/roadmap-v4/STATUS.md`

## Acceptance

- All scenarios pass on a clean build.
- No living minor or close relative is partnered.
- No sim has two active partners.
- The index matches components after restore.
- Household counts and vacancy agree after every merge or split.
- Replay is identical.
- The 10-year balance scenario and the existing suites remain green.

## Testing

- Full family suite.
- Persistence, snapshot and integration suites.
- 2,000-sim candidate/pairing timing.
- The 10-year balance report before and after V4 relationships.
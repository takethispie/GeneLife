# T08 — Traits acceptance

**Phase:** 8 — Verification
**Depends:** T01–T07
**Estimate:** 1 session
**Status:** Done

## Goal

Prove the trait foundation is complete, deterministic and durable before relationships and reproduction depend on it.

## Acceptance scenarios

### Fresh population

1. Generate 200 sims with a known seed.
2. Assert every living sim has one valid `Traits` component.
3. Assert every trait has a plausible distribution and no undefined value.
4. Assert the sex ratio is within tolerance.
5. Assert all creation paths are covered: seed, immigrant, player sim and admin sim.

### Determinism

1. Generate the same seed twice in separate worlds.
2. Assert identical traits for identical network ids.
3. Generate with a different seed and assert a different distribution.
4. Run the trait generator twice with the same per-sim key and compare.

### Inheritance

1. Inherit 10,000 children with mutation rate 0 and assert each trait equals one parent.
2. Repeat with a nonzero rate and assert the observed mutation rate is within tolerance.
3. Replay the same child id and assert identical results.
4. Test a single-value trait and a missing-parent fallback.

### Aptitude

1. Compare starting skills for `Science` and `Art` cohorts.
2. Run education and assert the matching family gains more.
3. Assert `Physical` leaves skills unchanged.

### Migration

1. Load a V5 fixture with no traits.
2. Migrate and assert every sim has traits and `TraitsMigrated`.
3. Restore the migrated state again and compare.
4. Corrupt an enum and assert the restore error.

### Runtime boundary

1. Assert no runtime project references `Microsoft.ML`.
2. Run the calibration tool separately and assert it writes the pinned fixture.
3. Assert the runtime coefficient version matches the fixture version.

## Tasks

- [ ] Add the trait acceptance suite under `Genelife.UnitTests/Family`.
- [ ] Add the V5 fixture and migration test.
- [ ] Add the deterministic population fixture.
- [ ] Add the aptitude cohort test.
- [ ] Add the runtime dependency assertion.
- [ ] Record the result in `roadmap-v4/STATUS.md` when implemented.

## Files

- `Genelife.UnitTests/Family/TraitAcceptanceTests.cs`
- `Genelife.UnitTests/Family/TraitMigrationTests.cs`
- `Genelife.UnitTests/Fixtures/*`
- `docs/plans/roadmap-v4/STATUS.md`

## Acceptance

- All scenarios pass on a clean build.
- No production creation path leaves a living sim without traits.
- Replay and restore are exact.
- The trait distribution is plausible but not uniform.
- The ML prototype remains an offline tool.
- Existing economy, housing and lifecycle tests remain green.

## Testing

- Full trait suite.
- Lifecycle, economy and persistence suites.
- A 2,000-sim trait-generation timing check.
- The 10-year balance scenario before and after V4 trait integration.
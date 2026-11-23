# H04 — Determinism and replay

**Phase:** 4 — Verification
**Depends:** features 01–04
**Estimate:** 2 sessions
**Status:** Planned (V4)

## Goal

Prove the same seed and options reproduce the same traits, relationships, births, inheritance and money over a decade.

## Context

`SimulationRandom` provides named streams. `Random.Shared` is banned. The V2 map and V4 systems both depend on stable ordering. A replay test is the only way to catch an accidental `Dictionary` iteration or a wall-clock dependency.

## Design

### Determinism contract

- All random draws use a named `SimulationRandom` stream.
- Pair-keyed rolls derive from stable network ids and the tick, not query order.
- Candidate and edge iteration sorts by network id.
- Attraction uses integer basis points.
- No wall-clock time, `Guid.NewGuid` in simulation state, or `Random.Shared`.
- Options and coefficients are persisted; a deploy does not change an existing save.
- Migration backfills use a stable seed + network id.

### Replay suite

1. Generate a 200-sim world with a known seed.
2. Run 10 years with births and inheritance.
3. Capture: traits, relationships, partnerships, births, deaths, inheritances, household composition, money supply, balance metrics.
4. Repeat in a fresh process/world.
5. Compare every captured value.
6. Repeat with seed `S+1` and assert the result differs.

### Seed sweep

- Run 20 seeds for 2 years.
- Assert no exceptions, invariants pass and population does not collapse.
- Record the worst-case tick time and candidate count.

### Migration replay

- Load a V5 fixture, migrate, run 1 year, save, restore, run again.
- Compare against a clean V6 run from the same seed.

### Failure diagnostics

- On a mismatch, report the first divergent tick, entity network id and field.
- Do not “fix” replay by reseeding mid-run.

## Tasks

- [ ] Add the replay capture/compare helper.
- [ ] Add the decade replay test.
- [ ] Add the seed sweep.
- [ ] Add the migration replay test.
- [ ] Add a static check for `Random.Shared` and wall-clock usage in family code.
- [ ] Add a divergence report.
- [ ] Record the result in `STATUS.md`.

## Files

- new `Genelife.UnitTests/Family/ReplayTests.cs`
- new `Genelife.UnitTests/Family/ReplayCapture.cs`
- `Genelife.UnitTests/Architecture/DeterminismTests.cs`
- `docs/plans/roadmap-v4/STATUS.md`

## Acceptance

- The decade replay matches on every captured field.
- A different seed produces a different result.
- The seed sweep has no invariant failure.
- Migration replay matches a clean run.
- No family code uses `Random.Shared` or wall-clock time.

## Testing

- Full replay suite.
- Seed sweep.
- Migration replay.
- Static determinism scan.
- Performance of the replay run.
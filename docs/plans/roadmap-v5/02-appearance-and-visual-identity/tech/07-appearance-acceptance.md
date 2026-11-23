# A07 — Appearance acceptance

**Phase:** 7 — Verification
**Depends:** A01–A06
**Estimate:** 1–2 sessions
**Status:** Planned (V5)

## Goal

Prove that appearance is deterministic, inherited, renderable, durable and within budget before V5 release.

## Acceptance scenarios

### Determinism

1. Derive the same descriptor twice from the same traits, age and version.
2. Change the seed and assert a different derived hair style where the stable draw is used.
3. Assert inherited traits never change because of the derived draw.

### Inheritance

1. Inherit `SkinTone` and `FaceShape` with mutation 0 and assert one parent's value.
2. Repeat with mutation and assert the observed rate is within tolerance.
3. Compare a parent, child and sibling; assert shared inherited fields.
4. Assert a new sim does not inherit the player's appearance.

### Aging

1. Age a sim through every band and snapshot the descriptor.
2. Assert hair greys and height scale changes without trait mutation.
3. Assert the player's appearance survives reconnect and heir continuation.

### Rendering

1. Render a representative set of descriptors in the browser.
2. Assert no per-sim texture request.
3. Assert the sprite pool reuses a descriptor hash.
4. Assert a missing atlas part falls back.
5. Assert local/other player markers are visible without colour.

### Payload and frame budget

1. Measure `SimStatus` with appearance for 2,000 sims.
2. Assert the delta remains smaller than the full snapshot for a one-sim change.
3. Measure composition time for a 2,000-sim viewport.
4. Assert the frame budget is met on the reference hardware.

### Migration

1. Load a schema 11 fixture with no appearance genes.
2. Migrate and assert deterministic genes and descriptors.
3. Restore again and compare.
4. Corrupt an enum and assert the restore error.

### Admin

1. Open `/appearance` with an empty and seeded world.
2. Preview a sim and a family.
3. Assert unauthorized access is rejected.
4. Assert a missing atlas part produces a diagnostic.

## Tasks

- [ ] Add the appearance acceptance suite.
- [ ] Add the schema 11 appearance fixture.
- [ ] Add the browser visual smoke test.
- [ ] Add the payload and frame-budget report.
- [ ] Record results in `roadmap-v5/STATUS.md` when implemented.
- [ ] Add the appearance section to the release checklist.

## Files

- `Genelife.UnitTests/Appearance/AppearanceAcceptanceTests.cs`
- `Genelife.UnitTests/Fixtures/v11-no-appearance.json`
- `clients/browser/e2e/appearance.spec.ts`
- `clients/browser/docs/appearance-performance.md`
- `docs/plans/roadmap-v5/STATUS.md`

## Acceptance

- Every scenario passes on a clean build.
- Appearance is deterministic across replay and restore.
- Family resemblance is visible and inherited.
- The client renders without per-sim assets and within budget.
- The admin preview matches the browser identity.
- No economy, family or persistence test regresses.

## Testing

- Full appearance suite.
- Family, persistence and client suites.
- 2,000-sim payload and frame measurement.
- Playwright appearance smoke.
- Schema 11 → 13 migration and double restore.

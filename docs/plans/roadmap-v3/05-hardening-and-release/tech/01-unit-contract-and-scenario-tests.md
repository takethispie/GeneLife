# H01 — Unit, contract and scenario tests

**Phase:** 1 — Verification
**Depends:** F02, R02, T02, T03, I02
**Estimate:** 2 sessions
**Status:** Planned (V3)

## Goal

Cover the client logic that can corrupt state without a browser: RLE decoding, map cache versions, delta application, interpolation, intent sequencing and viewport debounce.

## Context

A visual bug is obvious; a state bug is not. The client needs deterministic tests before Playwright, because E2E failures are slow and noisy.

## Design

Test layers:

| Layer | Tool | Focus |
|---|---|---|
| Pure functions | Vitest | RLE, flags, camera math, interpolation |
| Stores | Vitest | full replace, delta, removals, gaps |
| Network bridge | Vitest | ack ordering, stale/gap guards |
| Intent client | Vitest | payload, sequence, local rate limit |
| Contracts | Vitest + fixtures | camelCase, polymorphic intents, required fields |

Scenario fixtures:

- map snapshot with roads/buildings
- full world snapshot
- delta with changed/removed sims
- map delta with add/update/remove
- move and useBuilding intents
- `SnapshotRejected`

Rules:

- Every test is deterministic; no live clock.
- Time and tick duration are injected.
- No test depends on a live API; E2E does that in H02.
- A malformed fixture must fail loudly.

## Tasks

- [ ] Add test helpers for ticks, sims and map fixtures.
- [ ] Cover RLE valid/malformed/coverage cases.
- [ ] Cover full/delta/removal/gap store cases.
- [ ] Cover interpolation lerp/snap/sparse cases.
- [ ] Cover intent sequence and payload shape.
- [ ] Cover viewport debounce and clamp handling.
- [ ] Add coverage reporting with a minimum threshold.

## Files

- `clients/browser/src/test/fixtures/*`
- new `clients/browser/src/test/helpers/*`
- `clients/browser/src/**/*.test.ts`

## Acceptance

- Unit tests run without a server.
- A state-corrupting regression fails a test.
- Fixtures match the current server contracts.
- Coverage threshold is enforced in CI.
- No test relies on wall-clock timing.

## Testing

- `npm run test` green.
- Coverage report.
- A deliberately broken RLE/delta fixture fails as expected.

# T06 — Realtime acceptance

**Phase:** 6 — Verification
**Depends:** T01–T05
**Estimate:** 1–2 sessions
**Status:** Planned (V3)

## Goal

Prove the live client applies server state correctly under normal traffic, viewport changes, reconnects and map updates.

## Context

This is the first acceptance point with a moving world. It must catch ghost sims, double application, stale views and broken acknowledgement before input makes the client feel broken.

## Design

Run three scenarios:

1. **Fixture replay** — recorded full snapshot followed by recorded deltas.
2. **Live single player** — connect, move for a minute, change viewport, reconnect.
3. **Live two players** — two browsers, same world, independent viewports.

Measure:

- Applied ticks and deltas.
- Stale/rejected/gap counts.
- Ghost/removal checks.
- Interpolation delay and buffer depth.
- Reconnect success.

## Tasks

- [ ] Add fixture replay tests for full + delta + removal.
- [ ] Add a two-browser viewport test.
- [ ] Add a reconnect test with a forced socket drop.
- [ ] Add a map delta/gap test.
- [ ] Add a no-ghost assertion after full replacement.
- [ ] Record the debug overlay during acceptance.

## Files

- `clients/browser/src/net/*.test.ts`
- `clients/browser/src/simulation/*.test.ts`
- `clients/browser/e2e/realtime.spec.ts`

## Acceptance

- The client survives a 60-second live session without a stale store.
- Two players see independent viewports.
- A forced reconnect restores state without duplicates.
- A map delta applies; a map gap refreshes.
- No ghost sprite survives a full snapshot or removal.
- Debug metrics match the server’s tick rate within tolerance.

## Testing

- Fixture replay.
- Two-browser Playwright.
- Forced disconnect.
- Map delta/gap.
- Ghost/removal assertions.

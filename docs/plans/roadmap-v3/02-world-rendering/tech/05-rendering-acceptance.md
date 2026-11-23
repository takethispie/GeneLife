# R05 — Rendering acceptance

**Phase:** 5 — Verification
**Depends:** R01–R04
**Estimate:** 1 session
**Status:** Planned (V3)

## Goal

Prove the client renders the real map deterministically, within a frame budget, before live replication is added.

## Context

This stop point isolates rendering from networking. If the map is wrong here, live snapshots will hide the defect behind motion.

## Design

Use three inputs:

1. A small synthetic map for unit tests.
2. A recorded 256×256 `MapSnapshot` fixture for visual checks.
3. A 2,000-sim fixture snapshot for the render budget.

Record reference screenshots for the fixture map at fixed camera positions and zoom levels.

## Tasks

- [ ] Add the fixture map and reference screenshots.
- [ ] Add a deterministic rendering test at fixed camera/zoom.
- [ ] Add a 2,000-sim sprite budget check.
- [ ] Verify culling reduces rendered sims when zoomed in.
- [ ] Verify map version/debug overlay values.
- [ ] Document known visual limitations.

## Files

- `clients/browser/src/test/fixtures/map-snapshot.json`
- new `clients/browser/src/render/*.test.ts`
- new `clients/browser/src/test/screenshots/*`

## Acceptance

- The fixture map renders with correct bounds, roads and buildings.
- RLE coverage and building count match the fixture.
- A 2,000-sim fixture stays within the agreed frame budget on the reference machine.
- Culling visibly reduces draw work when zoomed in.
- Rendering does not depend on a live server.

## Testing

- Visual snapshot comparison.
- Frame-time budget run.
- Culling count assertions.
- Malformed map fixture failure.

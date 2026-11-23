# T03 — Interpolation and local player

**Phase:** 3 — Motion
**Depends:** T02
**Estimate:** 2 sessions
**Status:** Planned (V3)

## Goal

Render smoothly between authoritative ticks without inventing state: buffer snapshots by tick, interpolate sim positions, snap on correction, and make the local player unmistakable.

## Context

Sims carry `TileX/Y` and float `X/Y`. The server is authoritative. Broadcasts are frequent enough to interpolate but not to render raw updates without visible stepping. The client does not yet have a reliable own-sim id from the server; see the prerequisite in `STATUS.md`.

## Design

### Buffer

```ts
type SimFrame = { tick: number; sims: Map<number, SimView> };
const frames = new Map<number, SimFrame>();
```

- Keep the last N ticks (start with 1 second of history).
- Render at `estimatedServerTime - interpolationDelayMs` (start with 120 ms).
- Find the two frames surrounding render time.
- If only one frame exists, render it directly.
- If the gap exceeds the snap threshold, snap to the latest frame.

### Position

- Interpolate float `x/y` between the two frames.
- Keep tile coordinates for selection and intent targets.
- Do not extrapolate more than one tick.
- Snap on spawn, teleport, full snapshot, map change or an error above the threshold (start with 3 tiles).
- Never let interpolation move a sim through a building; a large correction snaps.

### Local player

- Get `ownSimNetworkId` from the server prerequisite (`Welcome` event or sim projection).
- Always render the local player even if a filtered snapshot omits others.
- Use a distinct marker/ring and keep it above other sims.
- If the id is missing, show a client error rather than guessing among `IsPlayerControlled` sims.

### Time

- Derive tick duration from configuration or the server contract; do not hardcode 40 ms.
- Use arrival time only to smooth clock drift, never to override server ticks.

## Tasks

- [ ] Implement the frame buffer and render-time selection.
- [ ] Implement lerp and snap rules.
- [ ] Implement local-player identification and marker.
- [ ] Add interpolation delay/snap configuration.
- [ ] Add a debug readout for buffer depth and delay.
- [ ] Add tests for sparse frames, large corrections and removals.

## Files

- new `clients/browser/src/simulation/interpolation.ts`
- new `clients/browser/src/render/sim-layer.ts`
- new `clients/browser/src/simulation/interpolation.test.ts`
- `STATUS.md` prerequisite: own sim network id

## Acceptance

- A sim moving across two ticks renders smoothly.
- A missing intermediate tick snaps rather than extrapolates wildly.
- A large correction snaps.
- The local player is always present and visually distinct.
- A removed sim disappears immediately.
- Render is independent of React/UI state.

## Testing

- Two-frame lerp test.
- Sparse-frame/snap test.
- Large-correction test.
- Own-sim marker test.
- Removal test.

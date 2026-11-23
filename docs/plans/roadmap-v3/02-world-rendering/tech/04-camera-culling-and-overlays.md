# R04 — Camera, culling and overlays

**Phase:** 4 — View
**Depends:** R03
**Estimate:** 1–2 sessions
**Status:** Planned (V3)

## Goal

Give the player a camera that pans, zooms and stays within map bounds, cull off-screen render work, and expose a debug overlay that proves what the renderer is doing.

## Context

The camera is also the source of the server viewport. It must be cheap, smooth and explicit. Culling is necessary at 2,000 sims and large maps even before art quality improves.

## Design

### Camera

- Maintain `centerX`, `centerY` and `zoom` in tile units.
- Apply one transform to the `world` container.
- Clamp the camera so the viewport does not leave map bounds at high zoom.
- Smooth pan with a reduced-motion override.
- Do not make the camera follow the player automatically in V3; expose it as an option later.

### Viewport

```ts
const visible = camera.visibleTileRect();
// convert to server contract
const request = {
  centerX: Math.round((visible.minX + visible.maxX) / 2),
  centerY: Math.round((visible.minY + visible.maxY) / 2),
  radius: Math.ceil(Math.max(visible.width, visible.height) / 2)
};
```

- Debounce viewport updates (start with 200 ms).
- Skip if the center/radius change is below a threshold.
- Use the `ViewportResult` center/radius as authoritative.

### Culling

- Mark layers/sprites `renderable = false` outside the visible rect plus a margin.
- Pool sim sprites and building labels.
- Never rebuild the tilemap on pan; only update culling state.

### Overlay

Show: FPS, frame time, map id/version, camera center/zoom, visible tile bounds, visible sims, snapshot tick, interpolation delay, payload bytes.

## Tasks

- [ ] Implement camera pan/zoom and bounds clamping.
- [ ] Implement visible tile rect calculation.
- [ ] Implement culling for buildings, sims and labels.
- [ ] Add sprite/label pools.
- [ ] Add the debug overlay.
- [ ] Add a reduced-motion camera mode.
- [ ] Emit the viewport model for the realtime feature.

## Files

- new `clients/browser/src/render/camera.ts`
- new `clients/browser/src/render/culling.ts`
- new `clients/browser/src/render/debug-overlay.ts`
- new `clients/browser/src/render/pools.ts`

## Acceptance

- Camera pan/zoom stays within bounds.
- Off-screen sprites are not rendered.
- Debug overlay reports map version, camera bounds and visible counts.
- Reduced-motion mode removes camera easing.
- Panning does not rebuild the tilemap.

## Testing

- Visible-rect math tests.
- Bounds/zoom clamping tests.
- Culling visibility tests.
- Reduced-motion preference test.

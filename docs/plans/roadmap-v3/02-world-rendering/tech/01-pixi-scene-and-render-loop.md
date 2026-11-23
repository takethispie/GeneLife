# R01 — Pixi scene and render loop

**Phase:** 1 — Rendering
**Depends:** F04
**Estimate:** 1 session
**Status:** Planned (V3)

## Goal

Boot one PixiJS application with explicit scene layers, a display-rate render ticker and a resize policy that behaves on high-DPI and low-power devices.

## Context

The client renders a large tile world plus dynamic sprites. A single application and a single world container keep camera and batching predictable. React, if added later, must sit outside this loop.

## Design

```ts
const app = new Application();
await app.init({
  background: "#101418",
  resizeTo: window,
  antialias: false,
  resolution: Math.min(window.devicePixelRatio || 1, 2),
  autoDensity: true
});

document.body.appendChild(app.canvas);

const world = new Container();
app.stage.addChild(world);

const layers = {
  terrain: new Container(),
  roads: new Container(),
  buildings: new Container(),
  sims: new Container(),
  selection: new Container(),
  effects: new Container()
};
Object.values(layers).forEach((layer) => world.addChild(layer));
```

Rules:

- The camera transform applies to `world`, not to every sprite.
- Render-only work happens in `app.ticker`; network callbacks enqueue work.
- Pause the ticker when `document.hidden` is true.
- Do not use `requestAnimationFrame` alongside the Pixi ticker.
- No React `setState` per frame.

## Tasks

- [ ] Initialize Pixi with DPR-aware resolution and resize handling.
- [ ] Add the world container and layer order.
- [ ] Add a render tick that consumes the simulation store.
- [ ] Add visibility pause/resume.
- [ ] Add a frame-time counter and a basic FPS readout.
- [ ] Dispose the application cleanly on hot reload.

## Files

- new `clients/browser/src/render/pixi-app.ts`
- new `clients/browser/src/render/layers.ts`
- new `clients/browser/src/render/render-loop.ts`
- new `clients/browser/src/app/bootstrap.ts`

## Acceptance

- A canvas fills the viewport and survives resize.
- Layer order is stable: terrain, roads, buildings, sims, selection, effects.
- The ticker pauses when the tab is hidden.
- A hot reload does not leak tickers or WebGL contexts.
- The render loop never mutates network state directly.

## Testing

- Bootstrap/dispose unit test.
- Resize smoke test.
- Visibility pause test.
- Manual high-DPI check.

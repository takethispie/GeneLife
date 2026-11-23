# I01 — Input map and camera controls

**Phase:** 1 — Input
**Depends:** R04, T04
**Estimate:** 1 session
**Status:** Planned (V3)

## Goal

Provide a predictable input layer for camera pan/zoom, hover, selection and future movement, without letting camera gestures submit intents by accident.

## Context

Pixi renders a canvas; the HUD is DOM. Input must work when the canvas has focus, must not scroll the page unexpectedly, and must be configurable for keyboard and pointer users.

## Design

### Input map

| Action | Default |
|---|---|
| Pan left/right/up/down | `A`/`D`/`W`/`S` and arrow keys |
| Zoom in/out | mouse wheel, `+`/`-` |
| Select | left click / `Enter` on focused target |
| Move to tile | left click on walkable tile |
| Cancel target | `Escape` |
| Toggle debug overlay | `F3` or a dev-only HUD button |

Keep bindings in one map so a future settings panel can remap them.

### Camera input

- Pan at a speed proportional to zoom.
- Zoom around the pointer position, clamped to min/max.
- Clamp the camera to map bounds at the current zoom.
- Respect `prefers-reduced-motion`; disable easing when set.
- Do not move the camera when the pointer is over a HUD control.

### Focus

- Canvas is focusable and shows a focus ring.
- Keyboard pan works only when the canvas or body is focused, not while typing in the HUD.
- `Tab` reaches HUD controls; `Escape` returns focus to the canvas.

## Tasks

- [ ] Add the input map and binding parser.
- [ ] Implement keyboard pan and zoom.
- [ ] Implement pointer pan/zoom and bounds clamping.
- [ ] Add canvas focus/keyboard handling.
- [ ] Add reduced-motion camera behaviour.
- [ ] Add a debug overlay toggle.
- [ ] Add input tests with synthetic events.

## Files

- new `clients/browser/src/input/input-map.ts`
- new `clients/browser/src/input/camera-input.ts`
- new `clients/browser/src/render/camera.ts`
- new `clients/browser/src/input/camera-input.test.ts`

## Acceptance

- Keyboard and pointer pan/zoom work.
- Camera cannot leave map bounds at high zoom.
- Wheel zoom does not scroll the page.
- HUD focus does not trigger camera keys.
- Reduced-motion disables camera easing.
- No camera gesture submits an intent.

## Testing

- Binding tests.
- Pan/zoom/clamp tests.
- Focus tests.
- Reduced-motion test.

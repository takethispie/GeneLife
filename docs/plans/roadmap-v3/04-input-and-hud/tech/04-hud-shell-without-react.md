# I04 — HUD shell without React

**Phase:** 4 — UI
**Depends:** I01, T05
**Estimate:** 2 sessions
**Status:** Planned (V3)

## Goal

Build a readable DOM/CSS HUD that reports session, connection, world, player, selection and errors, without introducing a frontend framework or putting the simulation into UI state.

## Context

The canvas is Pixi. The HUD needs crisp text, keyboard access and fast iteration. React is deliberately deferred. Redux is explicitly not used; the UI store is a tiny observable.

## Design

```text
#app
  canvas
  #hud
    connection-status
    player-card
    map-card
    selection-card
    notification-stack
    settings-panel
    debug-overlay (dev)
```

### UI store

- A small observable with `getState`, `setState`, `subscribe`.
- Stores: connection state, player id, map id/version, tick, fps, viewport radius, selection, last error, notification list.
- The network layer updates it at most 10 Hz; the HUD renders with `requestAnimationFrame` coalescing or a 100 ms timer.
- The simulation store is not copied into UI state.

### Rendering rules

- Use `textContent`, not `innerHTML`, for server strings.
- Keep the HUD outside the canvas for accessibility and crisp text.
- Use CSS variables for theme, contrast and HUD scale.
- No per-frame DOM updates; throttle and diff by value.

### Layout

- Top-left: connection and player.
- Top-right: map/version/tick/FPS (debug).
- Bottom-left: notifications.
- Bottom-right: selection/details.
- Center: transient target/action feedback.

## Tasks

- [ ] Add the HUD DOM shell and CSS layout.
- [ ] Implement the small UI store and subscription.
- [ ] Wire connection/player/map/tick/FPS values.
- [ ] Wire selection and notification panels.
- [ ] Add a debug overlay toggle.
- [ ] Add a 10 Hz update budget test.
- [ ] Ensure no server string is rendered as HTML.

## Files

- new `clients/browser/src/ui/ui-store.ts`
- new `clients/browser/src/ui/hud.ts`
- new `clients/browser/src/ui/hud.css`
- new `clients/browser/src/ui/hud.test.ts`

## Acceptance

- The HUD shows connection, player, map version, tick and FPS.
- HUD updates are coalesced and do not run per network event.
- Selection details survive snapshots and clear on removal.
- No React or Redux dependency is added.
- Server-provided strings are escaped.
- The HUD remains usable at browser zoom and keyboard-only navigation.

## Testing

- UI store subscription test.
- Throttle test.
- Selection lifecycle test.
- XSS/string-rendering test.
- DOM smoke test.

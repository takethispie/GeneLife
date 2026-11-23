# Input and HUD — Summary

**Folder:** `04-input-and-hud/`
**Status:** Planned (V3)

**Depends on:** realtime presence, world rendering, `SubmitIntent`, `MoveIntent`, `UseBuildingIntent`, `IntentResult`
**Feeds:** playable slice and hardening

## Objective

Let the player act: pan and zoom the camera, select a tile or building, submit server-authoritative move and interaction intents, and understand what happened through a DOM HUD, notifications and accessible status text.

## Baseline before V3

- The client can render and replicate the world.
- The server accepts `SubmitIntent(JsonElement)` and returns `IntentResult`.
- `MoveIntent` accepts `TargetTileX/Y`, `SimNetworkId`, `PlayerId` and `Sequence`; float `X/Y` are legacy.
- `UseBuildingIntent` carries `SimNetworkId` and `BuildingNetworkId`.
- The server rate-limits intents and validates ownership, bounds, reachability and roles.
- There is no input map, selection model, HUD or error surface.

## Deliverables

1. Keyboard and pointer input with a configurable key map.
2. Camera controls that do not accidentally submit intents.
3. Click-to-move and tile targeting with a visible target marker.
4. Intent submission with monotonic sequence, local rate limiting and typed results.
5. Building selection and `UseBuildingIntent` with success/failure feedback.
6. A DOM/CSS HUD with connection, tick, map version, FPS, viewport and player state.
7. Notification and settings panels, including reduced motion and keyboard focus.
8. An explicit no-React boundary; React remains an optional later HUD layer.

## Architecture

```text
Keyboard/Pointer
      │
      ├──► Camera controller ──► Viewport sync
      ├──► Selection model ──► HUD details
      └──► Intent controller
                │
                ├── MoveIntent(TargetTileX/Y, Sequence)
                └── UseBuildingIntent(BuildingNetworkId)
                          │
                          ▼
                    SubmitIntent ──► IntentResult
                          │
                          ▼
                    HUD notification
```

Input is translated into targets, never into authoritative positions. The server remains the only writer of world state.

## Tech steps

| # | Step | Phase |
|---|---|---|
| 01 | [Input map and camera controls](tech/01-input-map-and-camera-controls.md) | Input |
| 02 | [Intents, click-to-move and targeting](tech/02-intents-click-to-move-and-targeting.md) | Actions |
| 03 | [Building interaction and feedback](tech/03-building-interaction-and-feedback.md) | Interaction |
| 04 | [HUD shell without React](tech/04-hud-shell-without-react.md) | UI |
| 05 | [Notifications, settings and accessibility](tech/05-notifications-settings-and-accessibility.md) | UX |
| 06 | [Interaction acceptance](tech/06-interaction-acceptance.md) | Verification |

## Business reading

- [Controls, feedback and onboarding](business/01-controls-feedback-and-onboarding.md)
- [HUD without a framework](business/02-hud-without-a-framework.md)

## Definition of done

- WASD/arrow and pointer controls work without keyboard focus traps.
- Click-to-move sends a valid intent with a monotonic sequence.
- A rejected intent shows a specific, short reason and does not desynchronise the client.
- The local player has a visible selection and target marker.
- Building interaction is available only when the server accepts it; the client never asserts range.
- The HUD works with keyboard only and exposes connection, tick, FPS, map version and last error.
- Reduced-motion mode disables camera smoothing and interpolation-dependent animations where appropriate.
- No React dependency is required to ship the slice.

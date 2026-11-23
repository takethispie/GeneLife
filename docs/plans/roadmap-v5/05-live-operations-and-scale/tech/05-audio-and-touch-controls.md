# L05 — Audio and touch controls

**Phase:** 5 — Client
**Depends:** L04, V3 HUD and settings
**Estimate:** 2–3 sessions
**Status:** Planned (V5)

## Goal

Finish the client polish that V3 deferred: minimal audio cues and a usable touch/mobile control scheme, both optional and accessible.

## Context

V3 has a settings placeholder for audio, keyboard and pointer controls, and no mobile-specific scheme. V5 adds social notifications, player actions and world events that benefit from sound and from a control scheme that works without a keyboard.

Audio and touch must not become a second input authority. Every action still goes through the same server intents.

## Design

### Audio

- A small cue set: UI confirm/cancel, notification, chat, birth, death, utility outage and a soft ambient loop.
- Cues are short, mixed and loaded lazily.
- Audio is off by default until the player interacts, respecting browser autoplay policy.
- A master volume, mute and per-category toggle.
- Reduced-motion and high-contrast settings do not disable essential cues.
- No voice, no music streaming and no audio in the simulation tick.

### Touch controls

- Tap-to-move and tap-to-select.
- Pinch-to-zoom and drag-to-pan.
- A virtual joystick for camera or movement, configurable.
- Context action buttons for the nearest building or social action.
- Larger hit targets and a responsive HUD layout.
- A settings toggle for left/right handedness.

### Accessibility

- All touch actions have a keyboard/pointer equivalent.
- Audio cues have a visual notification equivalent.
- The HUD remains readable at mobile scale.
- Touch gestures do not trap the player in a mode.

## Tasks

- [ ] Add the audio cue manifest and lazy loader.
- [ ] Add volume/mute/category settings.
- [ ] Wire cues to UI, chat, birth, death and outage events.
- [ ] Add the touch input adapter and virtual controls.
- [ ] Add the responsive HUD layout and action buttons.
- [ ] Add the handedness and gesture settings.
- [ ] Add accessibility equivalents for cues and gestures.
- [ ] Add tests for input mapping and settings persistence.

## Files

- `clients/browser/src/ui/audio/*`
- `clients/browser/src/input/touch.ts`
- `clients/browser/src/ui/hud/*`
- `clients/browser/src/config/settings.ts`
- `clients/browser/e2e/mobile.spec.ts`

## Acceptance

- Audio is opt-in, respects autoplay policy and can be muted per category.
- Every audio cue has a visual equivalent.
- Touch can move, select, zoom, pan and trigger the context action.
- Touch actions submit the same intents as keyboard/pointer.
- The HUD is usable at mobile scale and in high-contrast mode.
- Settings persist per tab/session.
- No input path bypasses server authority.

## Testing

- Playwright mobile viewport: tap-to-move, pinch, action button.
- Assert the same intent sequence as the desktop path.
- Assert audio stays muted until interaction.
- Assert a cue has a visual fallback.
- Assert settings persist and reset.
- Run a touch soak with reconnect.

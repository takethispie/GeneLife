# I05 — Notifications, settings and accessibility

**Phase:** 5 — UX
**Depends:** I04
**Estimate:** 1–2 sessions
**Status:** Planned (V3)

## Goal

Make failures understandable and the client usable without a mouse or perfect vision: notifications, settings, focus management, contrast and reduced motion.

## Context

The client has many failure modes: auth, connection, map version, snapshot gap, rejected intent, rate limit and asset load. A silent failure makes the client look broken. Settings must not alter server authority.

## Design

### Notifications

- Short, typed messages: `info`, `success`, `warning`, `error`.
- Deduplicate repeated messages within a window.
- Include a code (`auth_expired`, `snapshot_gap`, `intent_rejected`) for diagnostics.
- Use an `aria-live="polite"` region; use assertive only for blocking failures.
- Do not show raw exception text to players.

### Settings

| Setting | Scope |
|---|---|
| HUD scale | display |
| Reduce motion | camera easing / decorative effects |
| Debug overlay | developer |
| Keybindings | local |
| Sound volume | future-compatible |
| Player id | session |

- Persist local-only settings.
- Do not expose interpolation delay, viewport radius or server tick as player settings.
- A settings change must not resubmit an intent or reset the connection.

### Accessibility

- Canvas has a label and a focus ring.
- All HUD controls are reachable by `Tab` and activated by keyboard.
- Status is announced, not only coloured.
- Contrast meets WCAG AA for HUD text.
- Reduced motion disables camera easing and non-essential effects.
- The game does not require precise pointer movement for basic actions; provide keyboard targeting where practical.

## Tasks

- [ ] Implement the notification queue and `aria-live` region.
- [ ] Add settings persistence and controls.
- [ ] Add keybinding remapping.
- [ ] Add focus management between canvas and HUD.
- [ ] Add contrast and reduced-motion CSS.
- [ ] Add an accessibility checklist to the client README.
- [ ] Add tests for deduplication and focus order.

## Files

- new `clients/browser/src/ui/notifications.ts`
- new `clients/browser/src/ui/settings.ts`
- new `clients/browser/src/ui/accessibility.css`
- new `clients/browser/src/ui/notifications.test.ts`

## Acceptance

- Errors are visible and announced.
- Repeated notifications do not spam the stack.
- Keyboard users can reach all HUD controls.
- Reduced motion is respected.
- Settings survive reload without affecting server state.
- No raw exception or token is shown.

## Testing

- Notification dedupe test.
- Focus order test.
- Reduced-motion test.
- Settings persistence test.
- Manual screen-reader smoke test.

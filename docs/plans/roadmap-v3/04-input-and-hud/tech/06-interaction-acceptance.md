# I06 — Interaction acceptance

**Phase:** 6 — Verification
**Depends:** I01–I05
**Estimate:** 1–2 sessions
**Status:** Planned (V3)

## Goal

Prove a player can pan, select, move, interact and recover from failures through the real hub and API.

## Context

This is the first vertical-slice acceptance. It exercises auth, map, replication, input, intents, HUD and reconnect together. It should be automated where possible and manually playtested for feel.

## Design

### Automated

- Playwright: authenticate with a test player, open the client, wait for map, click a walkable tile, assert an admitted move and a position change, select a building, attempt interaction, assert feedback.
- Force a socket drop and assert reconnect + HUD recovery.
- Submit a blocked target and assert no server position change and a visible message.

### Manual

- 10-minute playtest with a new player.
- Keyboard-only pass.
- Reduced-motion pass.
- Two-player viewport pass.

Record top confusion points and intent rejection codes.

## Tasks

- [ ] Add Playwright scenarios for move and interaction.
- [ ] Add blocked-target and reconnect scenarios.
- [ ] Add a keyboard-only checklist.
- [ ] Add a reduced-motion checklist.
- [ ] Run a two-player viewport session.
- [ ] Capture acceptance screenshots/logs.

## Files

- `clients/browser/e2e/interaction.spec.ts`
- `clients/browser/e2e/reconnect.spec.ts`
- `clients/browser/README.md`

## Acceptance

- A new player can authenticate, find their sim and move.
- A blocked move produces feedback and no false position.
- Building interaction gives honest success/failure.
- Reconnect restores input and HUD.
- Keyboard-only and reduced-motion passes are documented.
- Known limitations are recorded for the release checklist.

## Testing

- Playwright move/interact.
- Blocked target.
- Forced disconnect.
- Manual keyboard/reduced-motion.
- Two-player session.

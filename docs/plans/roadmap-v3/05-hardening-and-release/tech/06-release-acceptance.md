# H06 — Release acceptance

**Phase:** 6 — Verification
**Depends:** H01–H05
**Estimate:** 1 session
**Status:** Planned (V3)

## Goal

Decide whether the browser client is ready for a first playtest, with evidence rather than optimism.

## Context

V3 is a learning release. The acceptance bar is a playable, diagnosable vertical slice, not a finished game.

## Design

### Release gate

| Gate | Evidence |
|---|---|
| Build | CI artifact and typecheck/lint/test |
| Auth | Live player token and hub connect |
| Map | RLE coverage and correct fixture render |
| Realtime | Smooth sims, no ghosts, viewport works |
| Reconnect | Forced drop recovers |
| Input | Move, blocked target and interaction feedback |
| HUD | Connection/tick/FPS/version/errors visible |
| Performance | 2,000-sim budget recorded |
| Security | No token leak, untrusted strings escaped |
| Deploy | Preview URL with CORS and smoke test |

### Playtest checklist

- New player can orient within one minute.
- Movement feels acceptable at 120 ms interpolation.
- Reconnect does not require a reload.
- The top three intent failures are understandable.
- No console error storm.

## Tasks

- [ ] Run the release gate and attach evidence.
- [ ] Run a 10-minute new-player playtest.
- [ ] Record top confusion points and rejection codes.
- [ ] Decide go/no-go for the playtest.
- [ ] Write release notes and known limitations.
- [ ] File follow-ups for the next roadmap.

## Files

- `docs/plans/roadmap-v3/STATUS.md`
- new `clients/browser/RELEASE.md`

## Acceptance

- All gates pass or have an explicit, accepted waiver.
- A preview build is playable by someone who did not build it.
- Known limitations are documented.
- Follow-ups are filed before the next feature starts.

## Testing

- Full CI.
- Preview smoke test.
- Playtest.
- Release notes review.

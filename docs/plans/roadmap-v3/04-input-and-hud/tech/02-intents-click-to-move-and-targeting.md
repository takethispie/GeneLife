# I02 — Intents, click-to-move and targeting

**Phase:** 2 — Actions
**Depends:** I01, T02, T05
**Estimate:** 2 sessions
**Status:** Planned (V3)

## Goal

Translate pointer/keyboard targets into valid server intents, keep sequences monotonic, rate-limit locally and show admission results without pretending the move succeeded.

## Context

`SubmitIntent(JsonElement)` returns `IntentResult { success, error }`. `MoveIntent` carries `PlayerId`, `Sequence`, `SimNetworkId`, `TargetTileX`, `TargetTileY`; legacy float `X/Y` exist but new clients must not use them. The server validates ownership, bounds and walkability when it drains the queue. Post-admission failures are currently silent; see the `IntentRejected` prerequisite in `STATUS.md`.

## Design

### Intent payload

```json
{
  "type": "move",
  "playerId": "player-1",
  "sequence": 42,
  "simNetworkId": 12345,
  "targetTileX": 12,
  "targetTileY": -4
}
```

Rules:

- `sequence` is monotonic per player and must survive reconnect/reload. Persist a counter in `sessionStorage` and never reset below the last value.
- `simNetworkId` comes from the server’s own-sim identity prerequisite; do not guess from `IsPlayerControlled`.
- Send tile coordinates, not float positions.
- Reject local input when a move is already pending or the target is unchanged.
- Rate-limit locally below the server limit; do not retry a rejected intent in a loop.

### Targeting

- Hover highlights the tile under the pointer.
- Click on walkable tile shows a target marker and submits a move.
- Click on non-walkable tile shows a short “blocked” message and does not submit.
- Click on a building selects it; building interaction is I03.
- `Escape` clears the target marker.

### Result handling

- `IntentResult.success = true` means admitted, not arrived.
- `IntentResult.error` shows a short HUD message.
- Until `IntentRejected` exists, log unknown post-admission failures and show a generic “move not accepted” after a timeout.
- Do not mutate the local position from input.

## Tasks

- [ ] Implement the move intent builder with tile coordinates.
- [ ] Implement monotonic sequence persistence.
- [ ] Implement local intent rate limiting.
- [ ] Implement hover, target marker and click-to-move.
- [ ] Implement blocked/out-of-bounds local feedback.
- [ ] Handle `IntentResult` and prepare for `IntentRejected`.
- [ ] Add tests for sequence, payload and result handling.

## Files

- new `clients/browser/src/input/targeting.ts`
- new `clients/browser/src/net/intent-client.ts`
- new `clients/browser/src/input/targeting.test.ts`
- `STATUS.md` prerequisite: own sim id, `IntentRejected`

## Acceptance

- Clicking a walkable tile sends a valid `move` intent.
- Clicking a blocked tile sends nothing and shows feedback.
- Sequence never goes backwards across reload/reconnect.
- A rejected admission is visible and does not desynchronise state.
- The marker clears on `Escape` and after a full snapshot.
- No float `X/Y` fields are sent.

## Testing

- Payload shape test.
- Sequence persistence test.
- Blocked/out-of-bounds test.
- Rate-limit test.
- Result/notification test.

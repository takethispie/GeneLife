# T05 — Reconnect, acknowledgement and gap recovery

**Phase:** 5 — Resilience
**Depends:** T02, T03, T04
**Estimate:** 2 sessions
**Status:** Planned (V3)

## Goal

Recover a browser session after a dropped connection, expired token, missed delta or map-version gap without ghosts, duplicates or a frozen frame.

## Context

The server keeps a short snapshot history. `AcknowledgeSnapshot(tick)` records the last applied tick. `RequestSnapshot(long? sinceTick)` returns a delta if the tick is retained, otherwise a full snapshot. A viewport change invalidates the next delta and forces a full filtered snapshot. Map deltas have their own version chain.

## Design

### Acknowledgement

- Acknowledge only after the store has applied the tick.
- Never acknowledge on receipt.
- Keep `lastAppliedTick` monotonic.

### Reconnect

```text
connection.onreconnecting -> keep last frame, show status
connection.onreconnected  -> re-auth if needed, request state
connection.onclose        -> explicit disconnected state
```

State recovery order:

1. Ensure auth token is valid.
2. If map cache is missing or its version is stale, `RequestMapSnapshot`.
3. Call `RequestSnapshot(lastAppliedTick)` when the tick is recent enough; otherwise `RequestSnapshot(null)`.
4. Apply the full snapshot or delta.
5. Acknowledge the applied tick.

### Gap rules

| Condition | Action |
|---|---|
| `delta.sinceTick !== lastAppliedTick` | request full snapshot, do not apply |
| `delta.tick <= lastAppliedTick` | ignore stale delta |
| `mapDelta.fromVersion !== lastMapVersion` | request full map, pause dynamic apply |
| `SnapshotRejected` | back off locally, show status |
| auth expires | refresh and restart the hub |

### Visible behaviour

- Keep the last rendered frame while reconnecting.
- Show `Reconnecting…`, attempt count and a manual retry after failure.
- Do not clear the map cache on a transient disconnect.
- Do not leave a “connected” HUD while no events are arriving.

## Tasks

- [ ] Implement ack-after-apply.
- [ ] Implement reconnect recovery order.
- [ ] Implement stale/gap guards.
- [ ] Implement map-version recovery.
- [ ] Add reconnect counters and last error to diagnostics.
- [ ] Add a test that simulates dropped deltas and a map gap.
- [ ] Document the recovery contract in the client README.

## Files

- new `clients/browser/src/net/recovery.ts`
- `clients/browser/src/net/hub-connection.ts`
- `clients/browser/src/simulation/store.ts`
- new `clients/browser/src/net/recovery.test.ts`

## Acceptance

- A reconnect restores the world without ghost or duplicate sims.
- A missed delta requests a full snapshot.
- A map gap refreshes the map before new dynamic state is trusted.
- An expired token refreshes and reconnects.
- A failed reconnect is visible and retryable.
- The HUD never claims connected while the hub is closed.

## Testing

- Ack ordering test.
- Simulated disconnect/reconnect test.
- Missed-delta gap test.
- Map-version gap test.
- Token-expiry recovery test.

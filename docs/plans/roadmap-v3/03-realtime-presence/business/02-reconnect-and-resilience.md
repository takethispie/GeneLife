# Reconnect and resilience

A browser tab loses its connection when the network changes, the laptop sleeps or the server restarts. The client must recover without leaving the player in a broken world.

## What recovery means

- Keep the last rendered frame rather than flashing an empty canvas.
- Show a clear connection state and a retry path.
- Re-authenticate if the token expired.
- Request a full snapshot when the local tick is too old.
- Request a full map snapshot when the map version gap cannot be closed.
- Resume only after the new state has been applied.

## Why acknowledgement matters

The server keeps a short history. If the client acknowledges the last applied tick, a reconnect can receive a delta instead of a full snapshot. If the client lies or acknowledges a tick it did not apply, it can miss changes forever. Acknowledge only after applying.

## Why map versions are separate

Buildings change rarely. A map delta closes a single version gap; a larger gap must refresh the layout. Reusing a stale map is worse than a loading pause because every interaction target can be wrong.

## Product consequence

A player should be able to alt-tab, sleep the laptop, come back and continue. If recovery requires a manual page reload, the client is not ready for a playtest.

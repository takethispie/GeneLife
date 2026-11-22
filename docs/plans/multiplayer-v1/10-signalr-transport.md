# Step 10 — SignalR transport

**Depends:** 06, 09
**Goal:** Add realtime intent submission and snapshot push for 32 clients.

**Status:** Done

## Tasks

- [x] Add SignalR and a `WorldHub`.
- [x] Groups: `player-{PlayerId}`, `all`.
- [x] On connect: resolve `PlayerId`, send initial full snapshot, subscribe groups.
- [x] Receive intents: validate shape, enqueue to command queue, return errors.
- [x] On disconnect: clean up registry/subscriptions.
- [x] Add per-connection rate limits and payload size limits.

## Files likely touched

- new `Genelife.Api/Hubs/WorldHub.cs`
- `Genelife.Api/Program.cs`
- `Genelife.Api/Commands/*`

## Acceptance

- 32 clients connect and receive a correct initial snapshot.
- Intents flow to the command queue; malformed payloads are rejected.
- Disconnect cleans up state.

## Testing

- Connect/disconnect test; malformed payload test; rate-limit test.

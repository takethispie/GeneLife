# Realtime Presence — Summary

**Folder:** `03-realtime-presence/`
**Status:** Planned (V3)

**Depends on:** client foundation, world rendering, `WorldHub`, `WorldSnapshot`, `WorldSnapshotDelta`, `MapDelta`, viewport contracts
**Feeds:** input/HUD and the playable slice

## Objective

Turn the static scene into a live world: connect to `/hubs/world`, apply full snapshots and deltas to a mutable simulation store, interpolate sim positions, synchronise the camera viewport with the server, acknowledge ticks and recover cleanly from disconnects or gaps.

## Baseline before V3

- The server sends `SendMapSnapshot`, `SendMapDelta`, `SendSnapshot` and `SendSnapshotDelta`.
- The client has no SignalR connection, no entity store and no interpolation.
- `SetViewport` returns `ViewportResult` and a viewport change forces a fresh filtered snapshot.
- `RequestSnapshot(long? sinceTick)` and `AcknowledgeSnapshot(long tick)` support reconnect.
- Map deltas advance the map version; a gap should force a map refresh.
- The server broadcasts at 40 ms; 2,000 NPCs and 32 players are the target scale.

## Deliverables

1. A SignalR connection with auth, reconnect, logging and explicit states.
2. A mutable simulation store keyed by `NetworkId`, with full-replace and delta-apply paths.
3. Correct handling of `RemovedSims`, `RemovedCompanies`, map version changes and full snapshots.
4. An interpolation buffer with configurable delay, snap threshold and local-player priority.
5. Viewport synchronization from camera bounds, debounced and clamped by the server result.
6. Acknowledgement, gap detection, reconnect recovery and map-cache refresh.
7. A debug overlay for tick, array sizes, bytes, delay and rejected updates.

## Architecture

```text
HubConnection
   │  SendSnapshot / SendSnapshotDelta / SendMapSnapshot / SendMapDelta
   ▼
Network bridge ──► Replication store (mutable)
                         │
                         ├── MapCache (MapId + Version)
                         ├── Sims: Map<NetworkId, SimView>
                         └── Snapshot buffer by tick
                                  │
                                  ▼
                         Render tick interpolation
                                  │
                                  ▼
                              Pixi scene

Camera ──► viewport debounce ──► SetViewport ──► ViewportResult
Apply tick ──► AcknowledgeSnapshot(tick)
Reconnect ──► RequestSnapshot(null or last tick)
```

The store is not a UI store. It is a frame-oriented cache of server state.

## Tech steps

| # | Step | Phase |
|---|---|---|
| 01 | [SignalR client and hub lifecycle](tech/01-signalr-client-and-hub-lifecycle.md) | Transport |
| 02 | [Replication store and delta application](tech/02-replication-store-and-delta-application.md) | State |
| 03 | [Interpolation and local player](tech/03-interpolation-and-local-player.md) | Motion |
| 04 | [Viewport synchronization](tech/04-viewport-synchronization.md) | Interest |
| 05 | [Reconnect, acknowledgement and gap recovery](tech/05-reconnect-acknowledgement-and-gap-recovery.md) | Resilience |
| 06 | [Realtime acceptance](tech/06-realtime-acceptance.md) | Verification |

## Business reading

- [Server authority and smooth presence](business/01-server-authority-and-smooth-presence.md)
- [Reconnect and resilience](business/02-reconnect-and-resilience.md)

## Definition of done

- The client connects with a valid player token and receives a full map and snapshot.
- Full snapshots replace state atomically; deltas update only changed entities and remove deleted ones.
- Sims move smoothly with 100–150 ms interpolation and snap on spawn, teleport or large error.
- The local player is always present and visually distinct.
- Camera changes update the server viewport after a debounce; the returned result is respected.
- A map version gap triggers `RequestMapSnapshot` before new dynamic state is trusted.
- A disconnect recovers without duplicating, ghosting or freezing entities.
- The debug overlay makes tick, payload, entity count, delay and reconnect state visible.

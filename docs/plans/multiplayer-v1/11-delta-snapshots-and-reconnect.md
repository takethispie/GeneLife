# Step 11 — Delta snapshots and reconnect

**Depends:** 10
**Goal:** Reduce bandwidth and support reconnection.

**Status:** Done

## Tasks

- [x] Keep a ring buffer of recent snapshots (bounded by tick/time window).
- [x] Compute per-client deltas relative to the last tick the client acknowledged.
- [x] Send full snapshot on join or when the client is too far behind.
- [x] Handle `sinceTick` requests from clients.
- [x] Include position + tick for client interpolation.
- [x] Measure bytes per client per second.

## Files likely touched

- new `Genelife.Api/Snapshots/SnapshotHistory.cs`
- `Genelife.Api/Hubs/WorldHub.cs`

## Acceptance

- Reconnect within the window resumes with a delta.
- Reconnect outside the window gets a full snapshot.
- Bandwidth measurably lower than full snapshots.

## Testing

- Simulated packet loss and reconnect at various gaps.

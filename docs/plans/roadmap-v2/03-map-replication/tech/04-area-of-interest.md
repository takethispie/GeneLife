# R04 — Area of interest

**Phase:** 3 — Scale
**Depends:** P04, R02, M03
**Estimate:** 2–3 sessions
**Status:** Planned (V2)

## Goal

Filter dynamic sim replication to each connection's viewport while always including the player's own sim, and use the same viewport concept for map/building queries.

## Context

`AoiFilter` already filters a `WorldSnapshot` by a float radius (`Genelife.API/Snapshots/AoiFilter.cs`), but `SnapshotBroadcastService` never calls it. `ScalingTriggers` recommends `InterestManagement` when bytes per client exceed 64 KB/s.

The existing snapshot delta path compares full snapshots. AOI cannot be a naive post-filter of that delta: a sim entering the viewport may be absent from the delta because it did not change. The filter needs both the previous and current full snapshots.

## Design

### Viewport

```csharp
public readonly record struct Viewport(
    TileCoord Center,
    int Radius,
    int ExitMargin = 2)
{
    public bool Contains(TileCoord tile) =>
        Chebyshev(Center, tile) <= Radius + ExitMargin;
}
```

Rules:

- radius is clamped to a configured minimum/maximum;
- center must be in bounds (clamped if needed);
- exit margin creates hysteresis;
- the player's own sim is always included, even outside the radius;
- dead/removed sims are removed from the visible set.

### Filtering snapshots

```csharp
public static class AoiFilter
{
    public static List<SimStatus> FilterSims(
        WorldSnapshot snapshot,
        Viewport viewport,
        ulong? alwaysIncludeSim);

    public static WorldSnapshot FilterSnapshot(
        WorldSnapshot snapshot,
        Viewport viewport,
        ulong? alwaysIncludeSim);
}
```

The dynamic filter uses `SimStatus.TileX/TileY` and falls back to `X/Y` only for legacy snapshots. `SpatialIndex` is used by the engine when it can filter before building a snapshot; `AoiFilter` remains a pure post-filter for history and tests.

### Filtering deltas

`SnapshotBroadcastService` should build per-viewport deltas from two filtered snapshots:

1. get the previous full snapshot for the acknowledged tick from `SnapshotHistory`;
2. filter previous and current by the viewport;
3. diff the filtered sets;
4. include removals for sims that left the viewport or world;
5. include additions for sims that entered the viewport, even if unchanged;
6. always include the own sim.

Group connections by `(acknowledgedTick, viewportKey)` so one delta serves multiple clients. A viewport key is the rounded center/radius pair.

### Buildings

- The static `MapSnapshot` is sent in full by default; it changes rarely.
- `GET /api/map/viewport` and admin console viewport queries use the same viewport to return building AABBs.
- If map payload size becomes a measured problem, add a viewport-filtered map subscription in a later step. Do not mix building filtering into the per-tick sim delta.

### Hysteresis and refresh

- Enter at `Radius`.
- Leave at `Radius + ExitMargin`.
- Recompute the visible set on viewport change and on a bounded cadence.
- A client that never sets a viewport uses the controlled sim's tile and the default radius.

### Metrics

Record per connection:

- visible sims;
- filtered bytes;
- additions/removals caused by viewport movement;
- full-snapshot fallbacks;
- clients over the byte budget.

## Tasks

- [ ] Add `Viewport` and `Chebyshev` helpers.
- [ ] Extend `AoiFilter` with tile-aware sim filtering and own-sim inclusion.
- [ ] Add filtered-delta construction using previous/current snapshots.
- [ ] Group broadcasts by viewport key and acknowledged tick.
- [ ] Add hysteresis and refresh policy.
- [ ] Add own-sim and dead/removed handling.
- [ ] Add AOI metrics.
- [ ] Add tests for enter/leave, own sim, removed sim and delta additions.

## Files

- `Genelife.API/Snapshots/AoiFilter.cs`
- new `Genelife.API/Snapshots/Viewport.cs`
- `Genelife.API/Services/SnapshotBroadcastService.cs`
- `Genelife.API/Snapshots/SnapshotHistory.cs`
- `Genelife.API/Services/ReplicationMetrics.cs`
- `Genelife.UnitTests/Snapshots/AoiFilterTests.cs`

## Acceptance

- A sim inside the radius is visible; one outside is not.
- The own sim is always visible.
- A sim entering the viewport appears even if it did not change in the current tick.
- A sim leaving the viewport is sent as a removal.
- Hysteresis prevents boundary flicker.
- Grouping by viewport does not change the result for clients with the same viewport.
- AOI reduces payload bytes in a dense-world test.

## Testing

- Radius/edge tests.
- Own-sim inclusion.
- Enter/leave delta test.
- Hysteresis test.
- Removed/dead sim test.
- Grouped broadcast equivalence.
- Payload-size comparison with and without AOI.

# R03 — Building change events

**Phase:** 2 — Replication
**Depends:** M05, M07, R01, R02
**Estimate:** 2 sessions
**Status:** Planned (V2)

## Goal

Emit ordered, versioned building-change events so projections, map replication and clients agree on the current layout.

## Context

M05 introduces placement and removal. M09 introduces read models. R02 introduces map snapshots and deltas. Without a single ordered event stream, the document store, the live map and the client can disagree about which building exists.

## Design

### Events

```csharp
public sealed record MapGenerated(
    string MapId,
    int Version,
    int Seed,
    int Width,
    int Height,
    float TileSize,
    long Tick);

public sealed record BuildingPlaced(
    string MapId,
    int MapVersion,
    ulong NetworkId,
    string Kind,
    string Name,
    string District,
    int X,
    int Y,
    int Width,
    int Height,
    int EntryX,
    int EntryY,
    long Tick);

public sealed record BuildingRemoved(
    string MapId,
    int MapVersion,
    ulong NetworkId,
    long Tick);
```

A placement correction of an existing building is represented as `BuildingRemoved` followed by `BuildingPlaced` in the same map version, or as a `BuildingUpdated` event if the implementation prefers one row. V2 should choose one and keep it stable.

### Ordering

- `MapVersion` is monotonic and increments once per successful layout change.
- Events are emitted through `SimulationEngine.Emit` / the existing outbound event writer, never written to Marten on the tick thread.
- A `BuildingPlaced` event carries the version after the change, so projections and clients can order deltas.
- Duplicate event ids are ignored by projections (idempotent upsert/delete).
- A map version with no event is a bug; reconciliation reports it.

### Producers

| Producer | Event |
|---|---|
| Fresh world / restore | `MapGenerated` |
| Placement service | `BuildingPlaced` |
| Demolition/removal | `BuildingRemoved` |
| Failed admin placement | no map event; command result only |

`MapGenerated` should be emitted once per world generation, not once per process start if restoring an existing map. Restore may re-emit it for projections, but must not increment the version.

### Consumers

- `MapProjection` (R01) upserts/deletes documents.
- `SnapshotBroadcastService` (R02) uses the live `MapVersion` to send deltas; the event writer is not on the reply path.
- The admin map page may poll `/api/map` or receive `SendMapDelta`.
- Metrics count placements, removals and version changes.

### Failure handling

- If the event writer drops an event, the live map remains correct; the next full map snapshot or document rebuild repairs the read model.
- Dropped map events increment `DroppedEventCount` and produce an observability warning.
- A client that misses a delta requests a full map by version.

## Tasks

- [ ] Define `MapGenerated`, `BuildingPlaced`, `BuildingRemoved` and optional `MapVersionChanged`.
- [ ] Emit them from bootstrap, placement service and removal paths.
- [ ] Ensure map version is assigned before event emission.
- [ ] Make projections idempotent.
- [ ] Add map-event metrics and dropped-event checks.
- [ ] Add event ordering and version monotonicity tests.
- [ ] Add projection replay tests.
- [ ] Document the event-to-delta mapping.

## Files

- new `Genelife.Messages/Events/MapEvents.cs`
- `Genelife/Map/BuildingPlacementService.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.API/Projections/MapProjection.cs`
- `Genelife.API/Services/SnapshotBroadcastService.cs`
- `Genelife.UnitTests/Map/MapEventTests.cs`

## Acceptance

- A placement emits exactly one `BuildingPlaced` with the new map version.
- A removal emits exactly one `BuildingRemoved`.
- Map versions increase by one per change and never decrease.
- Replaying events reproduces the same documents.
- Duplicate events are harmless.
- A dropped event is counted and does not corrupt the live map.
- Map deltas sent to clients match the event changes.

## Testing

- Event payload tests.
- Version monotonicity.
- Projection replay/idempotency.
- Duplicate event test.
- Dropped-event metric test.
- Delta/event consistency test.

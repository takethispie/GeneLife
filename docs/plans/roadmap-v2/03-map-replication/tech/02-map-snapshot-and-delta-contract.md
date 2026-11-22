# R02 — Map snapshot and delta contract

**Phase:** 2 — Replication
**Depends:** R01, M09
**Estimate:** 2 sessions
**Status:** Planned (V2)

## Goal

Define a separate map replication channel: a full map snapshot on connect and a compact map delta when the layout changes.

## Context

`WorldSnapshot` currently contains only sims and companies. `WorldHub.OnConnectedAsync` sends `SendSnapshot`; `SnapshotBroadcastService` sends `SendSnapshotDelta`. Nothing sends map data.

Adding the full map to `WorldSnapshot` would repeat a large static payload on every reconnect and force map comparison into the per-tick delta path. V2 keeps the channels separate.

## Design

### Contracts

```csharp
public sealed class MapSnapshot
{
    public string MapId { get; init; } = string.Empty;
    public int Version { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public float TileSize { get; init; }
    public int MinX { get; init; }
    public int MinY { get; init; }
    public int MaxX { get; init; }
    public int MaxY { get; init; }
    public long Tick { get; init; }
    public List<MapBuildingStatus> Buildings { get; init; } = [];
}

public sealed class MapDelta
{
    public string MapId { get; init; } = string.Empty;
    public int FromVersion { get; init; }
    public int ToVersion { get; init; }
    public long Tick { get; init; }
    public List<MapBuildingStatus> Added { get; init; } = [];
    public List<MapBuildingStatus> Updated { get; init; } = [];
    public List<ulong> Removed { get; init; } = [];
}
```

### Connection sequence

`WorldHub.OnConnectedAsync` sends, in order:

1. `SendMapSnapshot` — static layout, if the client does not already have this `MapId`/version;
2. `SendSnapshot` — full dynamic world snapshot.

The client should apply the map first so building references in dynamic state resolve immediately. The map snapshot is cacheable; a reconnect with the same `MapId` and `MapVersion` can skip it after a `RequestMapSnapshot` check.

### Hub methods

| Method | Direction | Purpose |
|---|---|---|
| `SendMapSnapshot` | server → client | full static layout |
| `SendMapDelta` | server → client | layout change between versions |
| `RequestMapSnapshot` | client → server | ask for the map after a version gap |
| `AcknowledgeMapVersion` | client → server | optional cache confirmation |

`RequestMapSnapshot` should be rate-limited like `RequestSnapshot` and return a full snapshot if the requested version is no longer retained.

### Broadcast

`SnapshotBroadcastService` gains a map-version check:

- if `engine.MapVersion` is newer than the last broadcast map version, send `MapDelta` to clients that have the previous version;
- if a client is too far behind, send `SendMapSnapshot`;
- map broadcasts are independent of the 40 ms sim broadcast; they only run when the version changes.

### Versioning

- `MapVersion` increments on every successful placement or removal.
- `MapId` changes only when the world is regenerated.
- A client cache key is `(MapId, Version)`.
- A map delta that cannot apply cleanly forces a full snapshot.

### Snapshot history

Map changes are rare, so V2 does not need a full map snapshot ring for every tick. Keep a small `MapHistory` of the last N building versions or regenerate the delta from the placement log. If a client's version is outside the retained history, send a full map.

## Tasks

- [ ] Add `MapSnapshot`, `MapDelta` and map DTO conversion.
- [ ] Add `SendMapSnapshot`/`SendMapDelta`/`RequestMapSnapshot` hub methods.
- [ ] Send the map before the first world snapshot on connect.
- [ ] Add map-version-aware broadcast logic.
- [ ] Add full-map fallback on version gaps.
- [ ] Add map request rate limiting.
- [ ] Add client cache contract documentation.
- [ ] Add snapshot/delta serialization and fallback tests.

## Files

- new `Genelife.API/Snapshots/MapSnapshot.cs`, `MapDelta.cs`
- `Genelife.API/Hubs/WorldHub.cs`
- `Genelife.API/Services/SnapshotBroadcastService.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.API/Snapshots/MapHistory.cs`
- `Genelife.UnitTests/Snapshots/MapSnapshotTests.cs`

## Acceptance

- A connecting client receives a full map before or with its first world snapshot.
- A building change produces a map delta containing only that change.
- A client at an old/expired map version receives a full map.
- The dynamic snapshot never contains the full building list.
- Map request rate limiting and payload bounds apply.
- Reconnect with the current map version can skip the map payload.

## Testing

- Full map serialization.
- Add/update/remove delta tests.
- Version gap fallback.
- Connect ordering test.
- Request rate-limit test.
- Payload size comparison: map snapshot vs per-tick sim snapshot.

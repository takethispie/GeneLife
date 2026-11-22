# R05 — Viewport subscription

**Phase:** 3 — Scale
**Depends:** R04, P01
**Estimate:** 1–2 sessions
**Status:** Planned (V2)

## Goal

Let a client declare what it is looking at, validate that declaration, persist it for the connection, and use it for filtered replication and map queries.

## Context

The current hub registers a connection and sends snapshots. There is no concept of a camera or viewport. `AoiFilter` is dormant. Without a subscription, the server would have to guess from the player position, which is acceptable as a default but not enough for a client that pans the map or zooms out.

## Design

### Hub methods

```csharp
public sealed record SetViewportRequest(int CenterX, int CenterY, int Radius);

public Task<ViewportResult> SetViewport(SetViewportRequest request);
public void ClearViewport();
```

`ViewportResult` returns the effective clamped center/radius or a validation error.

### Registry

```csharp
public sealed class ViewportRegistry
{
    public bool TrySet(string connectionId, Viewport viewport);
    public Viewport? Get(string connectionId);
    public void Remove(string connectionId);
    public IReadOnlyDictionary<string, Viewport> All { get; }
}
```

- default viewport: controlled sim's tile + configured radius;
- explicit viewport: client-provided, clamped to bounds and radius limits;
- reconnect: restore the last viewport by `PlayerId`, or derive it from the sim;
- disconnect: remove connection state but retain the player's last viewport if useful for reconnect.

### Validation

| Condition | Result |
|---|---|
| Center out of bounds | clamp and report the effective center |
| Radius below minimum | clamp to minimum |
| Radius above maximum | clamp to maximum |
| Unknown connection | reject |
| Too many changes per second | rate limit |
| Simulation stopped | accept but do not broadcast until running |

### Broadcast integration

`SnapshotBroadcastService` groups connections by `(acknowledgedTick, viewportKey)`. A viewport change invalidates the client's next delta: send a fresh filtered snapshot or a delta that includes all newly visible sims.

### Map viewport

`GET /api/map/viewport` uses the same `Viewport` shape to return buildings intersecting the viewport. The admin map console can request a viewport rather than the full building list when the map is large.

## Tasks

- [ ] Add `SetViewport`/`ClearViewport` hub methods and `ViewportResult`.
- [ ] Add `ViewportRegistry`.
- [ ] Derive the default viewport from the controlled sim.
- [ ] Restore/derive the viewport on reconnect.
- [ ] Clamp and rate-limit viewport changes.
- [ ] Invalidate the next delta after a viewport change.
- [ ] Use viewport in `/api/map/viewport`.
- [ ] Add viewport tests and metric counters.

## Files

- `Genelife.API/Hubs/WorldHub.cs`
- new `Genelife.API/Snapshots/ViewportRegistry.cs`
- `Genelife.API/Services/SnapshotBroadcastService.cs`
- `Genelife.API/Endpoints/Wolverine/MapEndpoints.cs`
- `Genelife.API/Services/ReplicationMetrics.cs`
- `Genelife.UnitTests/Hubs/ViewportTests.cs`

## Acceptance

- A client can set and clear a viewport.
- Out-of-bounds centers and oversized radii are clamped predictably.
- The default viewport follows the controlled sim.
- A reconnect uses the restored or derived viewport.
- A viewport change causes the next update to include newly visible sims.
- The admin map viewport query uses the same bounds.
- Viewport method abuse is rate-limited.

## Testing

- Set/get/clear tests.
- Clamp and bounds tests.
- Rate-limit test.
- Default from player position.
- Reconnect restoration.
- Delta after viewport change.
- Map viewport query contract.

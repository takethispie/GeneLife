# P09 — Movement persistence and restore

**Phase:** 6 — Durability
**Depends:** P04, P05, M08
**Estimate:** 2 sessions
**Status:** Planned (V2)

## Goal

Persist authoritative positions and movement targets exactly, restore the derived movement state, and rebuild routes without serializing path arrays.

## Context

`PersistedSim` already stores `X/Y` floats. After V2, the authoritative position is `TilePosition`; `X/Y` becomes a derived interpolation value. A save can occur mid-edge, so restoring only the last completed tile would cause a visible jump or a lost movement order.

`MovementIndex` routes are derived and potentially large; they should not be persisted.

## Design

### Persisted fields

Extend `PersistedSim` with:

```csharp
public int TileX { get; set; }
public int TileY { get; set; }
public float SubTileX { get; set; }
public float SubTileY { get; set; }

public bool HasMoveTarget { get; set; }
public int MoveTargetX { get; set; }
public int MoveTargetY { get; set; }

public int MoveFromX { get; set; }
public int MoveFromY { get; set; }
public int MoveToX { get; set; }
public int MoveToY { get; set; }
public float MoveProgress { get; set; }
```

Keep `X/Y` for compatibility with older readers; write them as the derived world-space value. New readers prefer tile fields.

### Restore rules

1. Restore the map and buildings first (M08).
2. Restore `TilePosition`.
3. Validate the tile is in bounds and walkable.
4. If the sim is mid-edge and `MoveTo` is walkable, restore `From`/`To`/`Progress`.
5. If `MoveTo` is blocked or invalid, clear movement and leave the sim at its last valid tile.
6. If `TilePosition` itself is blocked, fail restore; migration repairs legacy V4 positions instead.
7. Restore `MoveTarget` if the target is still walkable and reachable.
8. Queue path requests for restored targets; do not restore routes.

A save/restore round-trip on an unchanged map must preserve the exact tile. Mid-edge progress may be rounded to the snapshot precision, but the next tick must continue from the persisted edge.

### Path cache

- Never persist `MovementIndex.Routes`.
- Reset `PathVersion` on restore.
- Rebuild cached entry routes lazily.
- If a route cannot be rebuilt, keep the target and leave the sim waiting for a new path request.

### Snapshot and delta

`SimStatus` gains:

- `TileX`, `TileY`;
- optional `MoveTargetX`, `MoveTargetY`;
- `X/Y` remains the interpolated world-space position.

`WorldSnapshotDelta.SimsEqual` includes the new fields so position and target changes are replicated.

### Backward compatibility

V4 sims have only `X/Y` and no tile. The V5 migration uses the map spawn/residence fallback (M08) to assign exact tiles. It must not treat `(0,0)` as valid without checking walkability.

## Tasks

- [ ] Add tile, target and mid-edge fields to `PersistedSim`.
- [ ] Capture/restore `TilePosition` and `MovementState`.
- [ ] Validate walkability on restore and fail on a blocked authoritative tile.
- [ ] Queue path requests for restored targets.
- [ ] Reset and rebuild the path cache.
- [ ] Extend `SimStatus` and delta equality with tile/target fields.
- [ ] Bump `WorldSnapshot.CurrentVersion` if the DTO shape changes and document the client contract.
- [ ] Add V4 position migration tests.
- [ ] Add exact round-trip and mid-route tests.

## Files

- `Genelife.API/Persistence/PersistedWorldState.cs`
- `Genelife.API/Services/SimulationEngine.cs` (`CaptureState`, `Restore`, `BuildSimSnapshots`)
- `Genelife.API/Snapshots/WorldSnapshotDelta.cs`
- `Genelife.API/DTOs/SimStatus.cs`
- `Genelife/Map/MovementIndex.cs`
- `Genelife.UnitTests/Persistence/MovementPersistenceTests.cs`

## Acceptance

- Save/restore preserves tile positions exactly.
- A mid-edge save resumes the same edge or leaves the sim on the last valid tile with a clear state.
- Restored targets are re-pathed.
- No route array is written to the snapshot.
- A blocked authoritative tile fails restore rather than silently teleporting.
- V4 positions are migrated to valid walkable tiles.
- Deltas include tile and target changes.

## Testing

- Exact position round-trip.
- Mid-edge round-trip.
- Target restore and re-path.
- Blocked target clears movement.
- Blocked authoritative position rejection.
- V4 `(0,0)` migration.
- Delta equality including new fields.

# P04 — Movement system and collision

**Phase:** 3 — Movement
**Depends:** P03, M03
**Estimate:** 2–3 sessions
**Status:** Planned (V2)

## Goal

Advance sims along their routes each tick, enforce map bounds and building collision, and keep the authoritative tile, float view and spatial index in sync.

## Context

There is no movement system today. `Location` is static at spawn. `SpatialIndex` exists but is not updated by the simulation. The client already receives `X/Y` and a tick, so interpolation is possible once the values change meaningfully.

## Design

### System placement

`MovementSystem`:

- order `40`, cadence `1`;
- runs after the command/intent drain and after `PathfindingSystem` (order `35`);
- queries `TilePosition`, `MoveTarget`, `MovementState`;
- ignores dead, player-disabled or routed-less sims;
- never allocates a route inside the query.

### Step model

```text
tileDuration = tileSize / speedTilesPerSecond
progressDelta = deltaTime / tileDuration
```

States:

- `IsMoving == false` and `HasTarget == false`: nothing.
- `HasTarget` but no route: wait for pathfinding.
- Route available: set `From`/`To` from the next route edge, `Progress` from 0.
- `Progress < 1`: update derived `Location` by linear interpolation.
- `Progress >= 1`: set `TilePosition = To`, update `SpatialIndex`, advance the route, reset progress.
- Final tile reached: clear target/route and emit `SimArrived`.

Sub-tile `Location` is presentation only. Collision decisions use `TilePosition` and the next tile, never the interpolated float.

### Collision rules

- The next tile must be inside `MapBounds`.
- The next tile must be walkable in `WorldMap`.
- Building footprints and water block movement.
- Dynamic sims do not block tiles in V2; they may overlap briefly. This keeps movement O(1) and avoids a crowd-simulation deadlock.
- If the next tile becomes blocked after a route is built (a building was placed), invalidate the route and emit `MoveBlocked`.

### Spatial index

When `TilePosition` changes:

- move the entity in `SpatialIndex`;
- update the tile-to-sim index used by AOI if one exists;
- do not emit a world event for every sub-tile step.

Emit `SimMoved` only when the tile changes or the target changes. The normal `SimNeedsChanged` path remains separate.

### Snapshot fields

`BuildSimSnapshots` should expose:

- `TileX`, `TileY` (authoritative);
- `X`, `Y` as world-space interpolation between tiles;
- `MoveTargetX`, `MoveTargetY` optionally;
- quantization remains at `0.01` for position.

### Determinism

- Use only `deltaTime` from the simulation context.
- Do not use wall-clock time.
- Route tie-breaking is fixed in P05.
- Iteration order is stable; movement does not depend on dictionary order.

## Tasks

- [ ] Add `MovementSystem` at order 40.
- [ ] Implement tile stepping, arrival and route advancement.
- [ ] Implement bounds/building collision and route invalidation.
- [ ] Update `SpatialIndex` on tile changes.
- [ ] Update snapshot DTOs with tile and target fields.
- [ ] Emit `SimMoved`/`SimArrived`/`MoveBlocked` events.
- [ ] Add a debug assertion that `Location` matches the derived tile position.
- [ ] Add movement tests with obstacles and zero/negative delta time.

## Files

- new `Genelife/Systems/MovementSystem.cs`
- `Genelife/Map/MovementIndex.cs`
- `Genelife/Components/Map/MoveTarget.cs`, `MovementState.cs`, `TilePosition.cs`
- `Genelife.API/Services/SimulationEngine.cs` (`BuildSimSnapshots`)
- `Genelife.API/DTOs/SimStatus.cs`
- `Genelife.Messages/Events/SimMoved.cs`, `SimArrived.cs`
- `Genelife.UnitTests/Systems/MovementSystemTests.cs`

## Acceptance

- A sim with a valid route moves one tile edge at a time and arrives at the target.
- A building or water tile stops movement with a `MoveBlocked` reason.
- A sim never leaves map bounds.
- `TilePosition`, derived `Location` and `SpatialIndex` agree after every tile change.
- No per-tick allocation is added for a moving sim.
- Save/restore mid-route resumes from the same tile and target.

## Testing

- Straight-line movement.
- Multi-tile route.
- Blocked next tile.
- Route invalidation after a placement.
- Bounds edge movement.
- Arrival clears state.
- Zero/negative delta guard.
- Spatial index equality.

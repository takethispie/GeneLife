# P05 — Pathfinding

**Phase:** 3 — Movement
**Depends:** P03, M03
**Estimate:** 2–3 sessions
**Status:** Planned (V2)

## Goal

Compute deterministic, budgeted routes on the walkable tile grid and store them outside ECS components.

## Context

V1 has no pathfinding. `SpatialIndex` provides neighbour queries but not a walkable grid. `WorldMap` provides `IsWalkable` and building occupancy after M03. Player movement needs routes around buildings; NPC commuting mostly does not.

## Design

### Algorithm and movement model

- A* on a 4-direction grid.
- Canonical neighbour order: north, south, west, east (or another fixed order).
- Tie-break by lower `f`, then lower `g`, then tile order. This makes equal-cost paths deterministic.
- Diagonal movement is out of scope for V2; it requires corner-cutting rules that are easier to add deliberately later.
- Maximum path length is configurable; beyond it, return `PathTooLong`.

### Request pipeline

```csharp
public readonly record struct PathRequest(
    Entity Entity,
    TileCoord Start,
    TileCoord Target,
    int MapVersion,
    int PathVersion);

public enum PathError
{
    None = 0,
    NoPath,
    StartBlocked,
    TargetBlocked,
    TooLong,
    BudgetExceeded
}
```

`PathfindingSystem`:

- order `35`, cadence `1`;
- drains a bounded number of requests per tick (default 32);
- processes requests in stable request-id order;
- writes the route into `MovementIndex`;
- increments `PathVersion` for that sim;
- leaves the request pending if the budget is exhausted.

Requests are created when a `MoveTarget` changes or a route is invalidated. Re-requesting the same start/target/version is deduplicated.

### Route cache

Cache routes by `(start, target, mapVersion, pathVersion)` for building entries and common destinations. Entry routes are cheap to reuse because many sims commute to the same office or school.

Invalidate the cache when:

- a building is placed or removed;
- a lot reservation changes walkability;
- the map version changes;
- the target building's entry changes.

### Budget and scheduling

- Player-controlled sims get requests first.
- NPC requests, if enabled, are lower priority and may use a separate budget.
- If the pathfinding share of tick time exceeds a threshold, reduce the per-tick budget before reducing movement correctness.
- `ScalingTriggers` already recommends scaling when decision/AI work dominates; pathfinding metrics must feed that decision.

### Storage

The route is stored in `MovementIndex`, never in `MoveTarget`/`MovementState`. Rebuild pending requests after restore from persisted targets.

## Tasks

- [ ] Add `TilePathfinder` and `PathRequest`/`PathError`.
- [ ] Implement A* with deterministic neighbour and tie-break order.
- [ ] Add `PathfindingSystem` at order 35.
- [ ] Add request queue, deduplication and per-tick budget.
- [ ] Store routes in `MovementIndex`.
- [ ] Add route cache with map/placement invalidation.
- [ ] Add pathfinding metrics (requests, failures, length, time).
- [ ] Add tests for shortest path, blocked target, no path, budget and determinism.

## Files

- new `Genelife/Map/TilePathfinder.cs`, `PathRequest.cs`, `PathError.cs`
- new `Genelife/Systems/PathfindingSystem.cs`
- `Genelife/Map/MovementIndex.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.UnitTests/Map/PathfindingTests.cs`

## Acceptance

- A path around a building is found and is the shortest 4-direction route.
- A blocked target returns `TargetBlocked`.
- An unreachable target returns `NoPath`.
- A request that exceeds the budget remains pending, not silently dropped.
- Equal-cost routes always choose the same path.
- Cache invalidation after a building placement forces recomputation.
- Pathfinding does not allocate a route inside an ECS component.

## Testing

- Straight and obstacle paths.
- Target/start blocked.
- No-path enclosure.
- Budget exhaustion and resumption.
- Deterministic tie-break.
- Cache hit/miss/invalidate.
- Performance smoke test with a dense building layout.

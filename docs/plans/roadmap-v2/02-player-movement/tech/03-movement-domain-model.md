# P03 — Movement domain model

**Phase:** 1 — Foundation
**Depends:** M01, P01
**Estimate:** 1–2 sessions
**Status:** Planned (V2)

## Goal

Define the ECS state for movement without putting managed route lists inside components, and keep one authoritative position per sim.

## Context

V1 sims have `Location` but no movement state. `ComponentHygieneTests` already enforces no `List<T>` fields on hot components. A path is variable-length, so it cannot live directly inside a blittable component.

## Design

### Components

```csharp
public record struct MoveTarget
{
    public TileCoord Tile;
    public bool HasTarget;
    public long RequestedTick;
    public int PathVersion;
}

public record struct MovementState
{
    public TileCoord From;
    public TileCoord To;
    public float Progress;      // 0..1 along the current tile edge
    public float SpeedTilesPerSecond;
    public bool IsMoving;
}

public record struct PlayerControlled
{
    public string PlayerId;
}
```

`TilePosition` from M01 remains authoritative. `Location` is derived from `TilePosition` plus the optional sub-tile progress for rendering.

### Derived movement index

```csharp
public sealed class MovementIndex
{
    public Dictionary<Entity, MovementRoute> Routes { get; }
    public Dictionary<Entity, PathRequest> PendingPaths { get; }
    public int PathVersion { get; }

    public void SetRoute(Entity sim, TileCoord[] route, int version);
    public bool TryGetNext(Entity sim, out TileCoord next);
    public void Clear(Entity sim);
    public void Rebuild(World world);
}
```

`MovementRoute` may use a `TileCoord[]` or a compact ring buffer. It is owned by the index, not by an ECS component. `PathVersion` invalidates a route when the map or target changes.

### State transitions

- No target: `MoveTarget.HasTarget == false`, `IsMoving == false`.
- Target accepted: `MoveTarget` set; a path request is queued.
- Path ready: `MovementIndex` stores the route; `MovementState` points at the next edge.
- Moving: `Progress` advances by `SpeedTilesPerSecond * deltaTime`.
- Arrival at the final tile: clear `MoveTarget`, set `IsMoving = false`, emit `SimArrived`.
- Target changed: increment `PathVersion`, clear the old route, queue a new request.
- Death/removal: remove from `MovementIndex`.

### Speeds

- Player speed is a per-world option; default should be tuned to the tick rate, not hard-coded in a system.
- NPC teleport commutes do not use `MovementState`; they write `TilePosition` directly at shift boundaries.
- Speed is clamped server-side; a component cannot be trusted if it comes from a client.

### Persistence boundary

Persist `TilePosition`, `MoveTarget` and speed. Do not persist the route array. Rebuild pending paths from the target on restore.

## Tasks

- [ ] Add `TilePosition`, `MoveTarget` and `MovementState` under `Genelife/Components/Map`.
- [ ] Add `MovementIndex` and `MovementRoute`.
- [ ] Add path-version invalidation helpers.
- [ ] Add movement-speed options.
- [ ] Add debug assertions for one target per sim and no route without a target.
- [ ] Extend component hygiene tests.
- [ ] Add restore/rebuild hooks for the index.
- [ ] Add transition tests.

## Files

- new `Genelife/Components/Map/TilePosition.cs`, `MoveTarget.cs`, `MovementState.cs`
- new `Genelife/Map/MovementIndex.cs`, `MovementRoute.cs`, `MovementOptions.cs`
- `Genelife.UnitTests/Components/ComponentTests.cs`
- `Genelife.UnitTests/Map/MovementIndexTests.cs`

## Acceptance

- Movement components contain no managed collections.
- A sim has at most one active target and one route.
- Changing a target invalidates the old route.
- Arrival clears movement state.
- The movement index rebuilds from persisted targets after restore.
- No system reads a route from an ECS component.

## Testing

- Target set/clear transitions.
- Route invalidation on target change.
- Arrival transition.
- Rebuild with pending targets.
- Component hygiene test.

# M03 — World map and occupancy

**Phase:** 1 — Foundation
**Depends:** M01, M02
**Estimate:** 2 sessions
**Status:** Planned (V2)

## Goal

Build one derived `WorldMap` index that answers placement, occupancy, walkability and building-lookup questions in constant or near-constant time, and can be rebuilt entirely from components after restore.

## Context

`SpatialIndex` already indexes dynamic entities by float position and supports neighbour and AABB queries, but it is not wired into the simulation. It is not an occupancy grid: it has no concept of a multi-tile building, a blocked tile, a road, a lot or an entrance.

The map needs a separate static index because buildings change rarely and occupy footprints, while sims move every tick.

## Design

### Map context

Add a `MapContext` owned by `SimulationEngine`, following the existing `EconomyContext` pattern:

```csharp
public sealed class MapContext
{
    public MapOptions Options { get; }
    public WorldMap Map { get; }
    public BuildingPlacementService Placement { get; }
    public SimulationRandom Random { get; }
}
```

`MapContext` is created in `SimulationEngine.Initialize` and restored before buildings are created.

### WorldMap structure

```csharp
[Flags]
public enum TileFlags : byte
{
    None = 0,
    Land = 1,
    Road = 2,
    Occupied = 4,
    Reserved = 8,
    Water = 16
}

public sealed class WorldMap
{
    private readonly MapOptions options;
    private readonly TileFlags[] tiles;             // row-major, offset by bounds
    private readonly Dictionary<NetworkId, Placement> placements;
    private readonly Dictionary<TileCoord, NetworkId> occupyingBuilding;

    public bool IsWalkable(TileCoord tile);
    public bool CanPlace(BuildingKind kind, Footprint footprint, TileCoord anchor, string zone);
    public Placement Place(NetworkId id, BuildingKind kind, Footprint footprint, TileCoord anchor, TileCoord entry);
    public bool Remove(NetworkId id);
    public bool TryGetPlacement(NetworkId id, out Placement placement);
    public NetworkId BuildingAt(TileCoord tile);
    public IEnumerable<NetworkId> BuildingsInAabb(MapBounds bounds);
    public void Rebuild(World world);
}
```

A dense array is preferred for the bounded default map. If the map is configured larger than the dense budget, fall back to a `Dictionary<TileCoord, TileFlags>` behind the same interface.

### Tile layers

- `Land` / `Water` comes from generation.
- `Road` marks pedestrian access.
- `Occupied` marks every tile covered by a building footprint.
- `Reserved` marks a lot or construction reservation.
- Buildings also reserve their entry tile against other buildings, not against walkability.

Occupancy tracks building footprints only. Dynamic sims do not occupy tiles in this grid; they are indexed by `SpatialIndex` so a person never blocks a tile permanently.

### Placement lookup

Keep two maps:

- `NetworkId → Placement` for persistence, replication and admin reads.
- `TileCoord → NetworkId` for collision and click/tile lookups.

Both are derived. Neither is serialized.

### Apply-phase discipline

`WorldMap.Place` and `WorldMap.Remove` are called only from:

- command application at tick start;
- initial bootstrap;
- construction completion/cancellation;
- restore rebuild.

They are never called inside an ECS query callback. The engine's write lock already serializes these paths.

### Rebuild and reconcile

`WorldMap.Rebuild(world)`:

1. clears all derived maps;
2. queries every entity with `Placement` and `NetworkId`;
3. validates bounds and footprint;
4. reserves each footprint;
5. verifies no duplicate tile ownership;
6. reports structured issues instead of throwing during diagnostics.

Restore uses the same method and refuses to start if reconciliation fails.

## Tasks

- [ ] Add `MapContext` and `WorldMap` under `Genelife/Map`.
- [ ] Add `TileFlags` and the dense/fallback tile storage.
- [ ] Implement placement, removal, lookup and AABB queries.
- [ ] Implement `Rebuild` from `Placement` components.
- [ ] Add `Reconcile`/`Verify` with structured issues.
- [ ] Wire `MapContext` into `SimulationEngine.Initialize` and shutdown.
- [ ] Allocate the grid once; assert no per-tick allocation in occupancy queries.
- [ ] Add map tests for overlap, removal and rebuild.

## Files

- new `Genelife/Map/MapContext.cs`, `WorldMap.cs`, `TileFlags.cs`
- new `Genelife.UnitTests/Map/WorldMapTests.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.API/Persistence/PersistedWorldState.cs` (referenced by restore in M08)

## Acceptance

- A placement reserves every tile in its footprint and rejects an overlapping placement.
- Removing a building releases every tile it owned.
- `IsWalkable` is false for building footprints and water, true for roads and free land.
- Rebuild from component state reproduces the same occupancy as the live index.
- Reconciliation reports overlap, out-of-bounds and duplicate-network-id issues.
- Occupancy queries allocate nothing after initialization.

## Testing

- Single and multi-tile placement/removal.
- Overlap rejection and full release on removal.
- Rebuild equality after arbitrary place/remove sequences.
- Tile lookup at footprint corners and outside bounds.
- AABB query returns only overlapping buildings.
- Reconcile catches a deliberately corrupted placement.

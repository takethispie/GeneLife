# M08 — Map persistence and restore

**Phase:** 4 — Durability
**Depends:** M03, M06
**Estimate:** 2–3 sessions
**Status:** Planned (V2)

## Goal

Persist the map layout and every building placement, restore them exactly, and migrate V4 snapshots so no building or sim is left at the origin.

## Context

`PersistedWorldState.CurrentSchemaVersion` is 4. `PersistedSim` already stores `X/Y`, but property, office and school records have no position. Restore currently recreates buildings without spatial context, and every legacy sim position is effectively `(0,0)`.

The map itself is generated, but restore cannot assume that regenerating from the same seed is sufficient forever: generator code can change. The saved map layout is part of the durable state.

## Design

### Snapshot schema V5

```csharp
public sealed class PersistedWorldState
{
    public const int CurrentSchemaVersion = 5;

    public PersistedMap Map { get; set; } = new();
    public List<PersistedPlacement> Placements { get; set; } = [];
    // existing sections remain
}

public sealed class PersistedMap
{
    public string MapId { get; set; } = "world";
    public int Version { get; set; } = 1;
    public int Seed { get; set; } = 1;
    public int Width { get; set; } = 256;
    public int Height { get; set; } = 256;
    public float TileSize { get; set; } = 1f;
    public byte[] Tiles { get; set; } = [];          // canonical TileFlags
    public List<PersistedLot> Lots { get; set; } = [];
}

public sealed class PersistedLot
{
    public int Id { get; set; }
    public int MinX { get; set; }
    public int MinY { get; set; }
    public int MaxX { get; set; }
    public int MaxY { get; set; }
    public string Zone { get; set; } = "Mixed";
}

public sealed class PersistedPlacement
{
    public ulong NetworkId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int EntryX { get; set; }
    public int EntryY { get; set; }
    public int Rotation { get; set; }
}
```

`PersistedSim` gains:

- `TileX`, `TileY` — authoritative integer position;
- `SubTileX`, `SubTileY` — optional interpolation offset;
- `MoveTargetX`, `MoveTargetY`, `HasMoveTarget`.

### Restore order

1. Create `MapContext` from `PersistedMap`; validate dimensions and tile array length.
2. Restore `WorldMap` tiles and lots.
3. Create companies, properties, offices and schools with their domain fields.
4. Apply `PersistedPlacement` rows, attaching `Placement` and reserving footprints.
5. Create sims with `TilePosition`, derived `Location` and movement target.
6. Restore all V1 relationships, accounts and indices.
7. Rebuild `WorldMap`, `SpatialIndex` and `MovementIndex`.
8. Reconcile: no overlaps, no unplaced building, no out-of-bounds sim, no blocked sim position.
9. Refuse restore with a specific error if any invariant fails.

Never persist `Entity` values. Persist `NetworkId` and resolve after the network-id registry is rebuilt.

### V4 → V5 migration

Create `MapMigrationService`:

1. Build a map from the V4 `RandomSeed` and default `MapOptions`.
2. Place properties in stable network-id order, then offices, then schools.
3. Place sims at their residence entry when a residence exists; otherwise use the nearest deterministic walkable spawn point.
4. Set `MigratedFromVersion = 4`.
5. Write V5 on the next save.
6. Fail with a clear error if a building cannot be placed; never drop it.

Legacy tests use fixed V4 fixtures and assert exact post-migration placements.

### Static map document

Also expose the persisted map as a Marten `MapDocument`/`BuildingDocument` for the API in M09. The snapshot remains the authoritative save; documents are read models and may be rebuilt from events.

## Tasks

- [ ] Bump schema to 5 and add `PersistedMap`, `PersistedLot`, `PersistedPlacement`.
- [ ] Add tile and movement fields to `PersistedSim`.
- [ ] Implement map capture and restore.
- [ ] Apply placements before sims and relationships.
- [ ] Rebuild and reconcile map/spatial/movement indices after restore.
- [ ] Implement V4 → V5 migration with deterministic backfill.
- [ ] Add V4 fixture migration tests.
- [ ] Add exact save/restore tests for placements and positions.
- [ ] Add corrupt-map rejection tests.

## Files

- `Genelife.API/Persistence/PersistedWorldState.cs`
- new `Genelife.API/Persistence/MapMigrationService.cs`
- `Genelife.API/Persistence/SnapshotMigrations.cs`
- `Genelife.API/Services/SimulationEngine.cs` (`CaptureState`, `Restore`)
- `Genelife.API/Services/WorldSaveService.cs`, `WorldRestoreService.cs`
- `Genelife/Map/MapContext.cs`, `WorldMap.cs`
- `Genelife.UnitTests/Persistence/MapPersistenceTests.cs`

## Acceptance

- A save/restore round-trip preserves map metadata, every placement and every sim tile exactly.
- Occupancy, spatial and movement indices rebuilt after restore match live indices.
- A V4 snapshot migrates without losing a building or leaving any entity at `(0,0)`.
- Restore refuses a snapshot whose placement overlaps, lies outside bounds or points at a missing network id.
- The map layout restores from persisted tiles even if the generator implementation changes.

## Testing

- Full snapshot round-trip.
- V4 fixture migration.
- Placement overlap rejection on restore.
- Missing building network-id reference.
- Sim inside a building footprint rejection.
- Rebuild-after-restore equality.
- Static map document rebuild.

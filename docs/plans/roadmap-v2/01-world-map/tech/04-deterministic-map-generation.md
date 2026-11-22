# M04 — Deterministic map generation

**Phase:** 2 — Generation
**Depends:** M01, M03
**Estimate:** 2–3 sessions
**Status:** Planned (V2)

## Goal

Generate a repeatable map structure from a seed: land, roads, blocks, lots and zones. The generator stops before placing buildings; placement belongs to M05 and M06.

## Context

Buildings cannot be placed reliably on an empty plane. Construction and admin commands need lots with known capacity, and movement needs roads and walkable ground. Without a generated structure, every building creator would invent its own placement rules.

## Design

### Generation options

```csharp
public sealed record MapGenerationOptions
{
    public int RoadSpacing { get; init; } = 12;
    public int BlockPadding { get; init; } = 2;
    public float WaterFraction { get; init; } = 0.04f;
    public int MinLotTiles { get; init; } = 12;
    public int ResidentialShare { get; init; } = 50;
    public int CommercialShare { get; init; } = 25;
    public int EducationShare { get; init; } = 10;
    public int CivicShare { get; init; } = 15;
}
```

All options are part of the map version contract. Changing a default requires bumping `MapVersion`, otherwise a saved map and a regenerated map would disagree.

### Generated records

```csharp
public enum MapZone { Residential, Commercial, Education, Civic, Mixed }

public readonly record struct LotId(int Value);

public sealed class MapLot
{
    public LotId Id { get; init; }
    public MapBounds Bounds { get; init; }
    public MapZone Zone { get; init; }
    public bool IsReserved { get; set; }
    public NetworkId? ReservedBy { get; set; }
}

public sealed class GeneratedMap
{
    public MapOptions Options { get; init; }
    public TileFlags[] Tiles { get; init; }
    public List<MapLot> Lots { get; init; }
    public List<TileCoord> SpawnPoints { get; init; }
    public string CanonicalSignature { get; init; }
}
```

### Generation phases

1. **Terrain** — mark land and water using a `SimulationRandom.For("map-generation")` stream.
2. **Roads** — carve a deterministic road grid using `RoadSpacing`; roads are walkable and give adjacent lots access.
3. **Blocks** — the land between roads becomes blocks.
4. **Lots** — split blocks into lots with padding; discard lots below `MinLotTiles` or with no road-adjacent edge.
5. **Zones** — assign zones by stable lot index and configured shares; reserve a small number of civic and education lots.
6. **Spawn points** — choose deterministic walkable tiles near the map centre and near residential lots.
7. **Signature** — compute a canonical string/hash over bounds, tile flags, lot bounds/zones and spawn points for determinism tests.

### Determinism rules

- No `Random.Shared`; use only `SimulationRandom.For(...)`.
- Do not iterate dictionaries when output order matters; sort by `TileCoord` or `LotId`.
- Use integer arithmetic for lot bounds and road placement.
- Do not include wall-clock time, ECS entity ids or hash codes in the generated output.
- `CanonicalSignature` must be stable across machines and runs.

### Relationship to buildings

The generator does not create `Property`, `Office` or `School`. It creates the ground and lots those entities will be placed on. M05 consumes `MapLot` records; M06 performs the initial building bootstrap.

## Tasks

- [ ] Add `MapGenerationOptions`, `MapZone`, `MapLot`, `GeneratedMap` and `MapGenerator`.
- [ ] Implement terrain, roads, blocks, lots, zones and spawn points.
- [ ] Add canonical signature generation.
- [ ] Add deterministic lot lookup by zone and capacity.
- [ ] Add configuration binding for generation options.
- [ ] Persist map options/version alongside the seed.
- [ ] Add tests for same-seed equality and different-seed variation.
- [ ] Add debug rendering or dump output for map inspection.

## Files

- new `Genelife/Map/MapGenerationOptions.cs`, `MapGenerator.cs`, `GeneratedMap.cs`, `MapLot.cs`, `MapZone.cs`
- `Genelife/Map/MapContext.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.UnitTests/Map/MapGeneratorTests.cs`

## Acceptance

- Same seed and options produce the same `CanonicalSignature`.
- Different seeds produce different layouts for the default options.
- Every generated lot lies inside bounds and contains at least `MinLotTiles`.
- Every building lot has at least one road-adjacent edge.
- Water tiles are never chosen as spawn points or building anchors.
- Regenerating after restore with the persisted seed/version reproduces the saved layout.

## Testing

- Determinism test over 100 runs.
- Seed variation test.
- Bounds and lot-capacity tests.
- Road-adjacency test.
- Spawn-point walkability test.
- Canonical signature stability test.

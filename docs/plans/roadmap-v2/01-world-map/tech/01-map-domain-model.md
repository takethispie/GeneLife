# M01 — Map domain model

**Phase:** 1 — Foundation
**Depends:** V1 complete
**Estimate:** 1–2 sessions
**Status:** Planned (V2)

## Goal

Define the canonical coordinate, bounds and footprint types for the spatial world, and make the authoritative tile position explicit without breaking the existing float snapshot contract.

## Context

`Location` is a `record struct` holding `System.Numerics.Vector2` (`Genelife/Components/Gen/Location.cs`). Every sim creation path writes `new Location(0f, 0f)`. `SpatialIndex` uses float `WorldBounds` and a float cell size (`Genelife/Systems/SpatialIndex.cs`). `SimStatus.X/Y` and `PersistedSim.X/Y` already expose float positions.

That contract is useful for rendering and interpolation but unsuitable for placement and collision: float arithmetic drifts, equality is awkward, and a tile index cannot be persisted as a negative/positive integer range cleanly unless it is explicit.

## Design

### Value types

```csharp
public readonly record struct TileCoord(int X, int Y);

public readonly record struct Footprint(int Width, int Height)
{
    public int Area => Width * Height;
    public bool IsValid => Width > 0 && Height > 0;
}

public readonly record struct MapBounds(int MinX, int MinY, int MaxX, int MaxY)
{
    public int Width => MaxX - MinX + 1;
    public int Height => MaxY - MinY + 1;
    public bool Contains(TileCoord tile) =>
        tile.X >= MinX && tile.X <= MaxX &&
        tile.Y >= MinY && tile.Y <= MaxY;
}
```

### Map options

```csharp
public sealed record MapOptions
{
    public string MapId { get; init; } = "world";
    public int Version { get; init; } = 1;
    public int Seed { get; init; } = 1;
    public int Width { get; init; } = 256;
    public int Height { get; init; } = 256;
    public float TileSize { get; init; } = 1f;

    public MapBounds Bounds => new(
        -(Width / 2),
        -(Height / 2),
        Width - 1 - (Width / 2),
        Height - 1 - (Height / 2));
}
```

The default map is centred on the origin to match the existing world-space convention. A client that expects top-left rendering subtracts `MinX`/`MinY`; the server never mixes the two conventions.

### Authoritative and derived position

Add a new component:

```csharp
public record struct TilePosition
{
    public TileCoord Tile;
    public TilePosition(TileCoord tile) => Tile = tile;
}
```

`TilePosition` is authoritative for simulation. `Location` remains the float world-space view and is derived in the same apply phase:

```csharp
world.Get<Location>(entity) = new Location(
    tile.X * options.TileSize,
    tile.Y * options.TileSize);
```

This avoids an all-at-once rewrite of snapshots and the client while giving every V2 system one integer source of truth. `Location` must never be mutated independently once `TilePosition` exists.

### Helpers

Add conversion and arithmetic helpers:

- `ToWorld(TileCoord, tileSize)` and `ToTile(Vector2, tileSize)` only at boundaries.
- `Contains`, `Clamp`, `Offset` for bounds.
- No implicit float-to-tile conversion in domain code.
- No `Vector2` in placement or collision math.

## Tasks

- [ ] Add `TileCoord`, `Footprint`, `MapBounds` and `MapOptions` under `Genelife/Map`.
- [ ] Add `TilePosition` under `Genelife/Components/Map`.
- [ ] Add conversion helpers and bounds tests.
- [ ] Decide and document that `TilePosition` is authoritative and `Location` is derived.
- [ ] Keep existing `Location(0f, 0f)` call sites compiling; migrate sim creation to `TilePosition` in P01.
- [ ] Add map options binding to `SimulationEngine` configuration.
- [ ] Add a lint/architecture test that placement code does not use float coordinates.

## Files

- new `Genelife/Map/TileCoord.cs`, `MapBounds.cs`, `Footprint.cs`, `MapOptions.cs`, `MapCoordinates.cs`
- new `Genelife/Components/Map/TilePosition.cs`
- `Genelife/Components/Gen/Location.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.UnitTests/Map/MapCoordinateTests.cs`

## Acceptance

- All canonical map types are value types with value equality.
- A tile converts to world space and back without drift for the configured tile size.
- Bounds contain, clamp and enumerate tiles deterministically.
- No new placement or collision code stores coordinates as `float`.
- Existing sim and snapshot code compiles unchanged.

## Testing

- Coordinate round-trip across negative and positive tiles.
- Bounds clamp and containment at edges.
- Footprint area and validity.
- Default `MapOptions` produces a centred 256 × 256 map.

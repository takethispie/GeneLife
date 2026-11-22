# M02 — Building placement components

**Phase:** 1 — Foundation
**Depends:** M01
**Estimate:** 1–2 sessions
**Status:** Planned (V2)

## Goal

Attach one authoritative placement to every building entity while preserving the existing domain identity of `Property`, `Office` and `School`.

## Context

The world has three building-bearing entity types:

- `Property` (`Genelife/Components/Housing/Property.cs`) with `Name`, `District`, `Owner`, `UnitCount`, condition and upkeep.
- `Office` (`Genelife/Components/Company/Office.cs`) with company ownership, capacity and operating cost.
- `School` (`Genelife/Components/Life/School.cs`) with district, capacity, quality and enrollment.

None has coordinates. `HousingUnit` already links to `Property`, `Employee` links to `Office`, and `Enrollment` links to `School`, so the natural place for a position is the building, not the occupants.

## Design

### Placement component

```csharp
public enum BuildingKind
{
    Residential,
    Office,
    School,
    Store,
    Civic
}

public record struct Placement
{
    public TileCoord Anchor;
    public Footprint Footprint;
    public TileCoord Entry;
    public BuildingKind Kind;
    public int Rotation; // 0..3, presentation only

    public int MinX => Anchor.X;
    public int MinY => Anchor.Y;
    public int MaxX => Anchor.X + Footprint.Width - 1;
    public int MaxY => Anchor.Y + Footprint.Height - 1;
}
```

`Placement` is attached to the same entity that owns the domain component. There is no separate "building entity" in V2; introducing one would duplicate ownership, names, accounts and registry entries. The placement is a shared spatial facet of an existing building.

### Inheritance rules

- `HousingUnit` never stores coordinates. Its address is `Property` plus `Placement`.
- Employees and job postings use `Office` plus `Placement`.
- Students and enrollment use `School` plus `Placement`.
- A building with no `Placement` is invalid after bootstrap; it must be explicitly unplaced only during migration.

### Entry rules

- `Entry` must lie on or immediately outside the footprint edge.
- `Entry` must be walkable and adjacent to a road or pedestrian access tile.
- Movement targets the entry, not the anchor. Interactions resolve by proximity to the entry.
- Multi-tile buildings may expose one entry in V2; multiple service points are a later extension.

### Rotation

Rotation is optional and presentation-oriented in V2. The footprint is already expressed in map axes after placement, so collision never depends on rotating a rectangle at query time. If rotation is used, the placement service computes the axis-aligned footprint before storing it.

## Tasks

- [ ] Add `BuildingKind` under `Genelife/Enums`.
- [ ] Add `Placement` under `Genelife/Components/Map`.
- [ ] Attach `Placement` to `Property`, `Office` and `School` creation paths.
- [ ] Add placement helper methods for footprint corners and tile enumeration.
- [ ] Update component hygiene tests to include `Placement`.
- [ ] Document that units and employees inherit building positions.
- [ ] Add debug assertions for entry-inside-footprint and positive footprint.
- [ ] Add a migration note for unplaced buildings.

## Files

- new `Genelife/Enums/BuildingKind.cs`
- new `Genelife/Components/Map/Placement.cs`
- `Genelife/Components/Housing/Property.cs`, `HousingUnit.cs`
- `Genelife/Components/Company/Office.cs`
- `Genelife/Components/Life/School.cs`
- `Genelife.UnitTests/Components/ComponentTests.cs`
- `Genelife.UnitTests/Components/ComponentHygieneTests.cs`

## Acceptance

- Every building creation path can attach exactly one `Placement`.
- `Placement` uses only value types and contains no managed collections.
- A footprint enumerates exactly `Width × Height` tiles.
- `Entry` validation rejects an entry outside the allowed edge and a non-walkable entry.
- Units, employees and enrollments resolve their building position without storing a copy.

## Testing

- Default and explicit placement construction.
- Footprint corner and tile enumeration.
- Entry inside/outside footprint tests.
- Inheritance test: a unit resolves its property's placement.
- Component hygiene test for `Placement`.

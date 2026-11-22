# M06 — Initial building bootstrap

**Phase:** 3 — Placement
**Depends:** M04, M05
**Estimate:** 2 sessions
**Status:** Planned (V2)

## Goal

Make the map exist before the first tick and ensure every initial building has a valid placement. Replace every implicit `world.Create(building)` path with the placement service.

## Context

`SimulationEngine.Initialize` creates the world and registers systems. The first buildings arrive through:

- `HousingBootstrap.CreateStock`, used by migration, seed and scenarios;
- `CompanyRepository.Add`, which can create a headquarters office;
- `HousingDevelopmentSystem`, which creates completed projects;
- `HousingRepository`, `OfficeRepository` and `SchoolRepository`;
- admin seed commands and tests.

Today those paths do not know about a map. After M05, they must request placement before or during entity creation.

## Design

### Startup order

`SimulationEngine.Initialize` becomes:

1. create `World`;
2. create `MapContext` from `MapOptions`;
3. generate the map from seed into `WorldMap`;
4. create economy/life indices;
5. create initial entities, placing every building through `BuildingPlacementService`;
6. register systems;
7. run a map reconciliation assertion in debug/test builds.

No system runs before the map exists.

### Bootstrap placement

`HousingBootstrap` should request lots by zone rather than choose coordinates:

```csharp
var stocks = new[]
{
    (HousingTier.Shelter, 6, MapZone.Civic),
    (HousingTier.Basic, 30, MapZone.Residential),
    (HousingTier.Comfortable, 12, MapZone.Residential),
    (HousingTier.Luxury, 3, MapZone.Residential)
};
```

For each property:

1. choose a compatible lot;
2. reserve it;
3. create the `Property` entity;
4. place it;
5. create its units and link them to the property.

If a tier cannot be placed, fail bootstrap with a structured error rather than reducing the stock silently.

### Offices and schools

- `CompanyRepository.Add` requests an office lot when a headquarters is requested.
- `OfficeRepository.Add` requires a district and uses `PlaceInZone`.
- `SchoolRepository.Add` requires an education/civic lot.
- Test-only construction should use the service or explicitly mark itself as unplaced and skip map assertions.

### Construction

`ConstructionProject` gains `LotId` and `PlacementReserved` fields.

- Start: reserve a lot and store the lot id on the project.
- Complete: create the property and place it on the reserved lot.
- Cancel: release the lot.
- Restore: rebuild the reservation from the project record.

This prevents the V1 failure mode where a project completes with nowhere to stand.

### Snapshot backfill

For V4 snapshots, M08 creates a deterministic `MapBackfillService`:

- regenerate the map from the persisted seed/version;
- place properties by district/zone in stable network-id order;
- place offices and schools next;
- fail restore if any building cannot be placed;
- record backfill count and map version in the restored state.

## Tasks

- [ ] Generate the map during `SimulationEngine.Initialize` before any building.
- [ ] Change `HousingBootstrap` to reserve and place through the service.
- [ ] Route all repositories through `BuildingPlacementService`.
- [ ] Add `LotId` and reservation state to `ConstructionProject`.
- [ ] Create HQ offices through the placement service.
- [ ] Implement deterministic backfill for unplaced V4 buildings in M08.
- [ ] Fail fast on an unplaceable initial building.
- [ ] Add bootstrap tests for full stock placement and lot exhaustion.

## Files

- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife/Housing/HousingBootstrap.cs`
- `Genelife/Systems/HousingDevelopmentSystem.cs`
- `Genelife/Components/Housing/ConstructionProject.cs`
- `Genelife.API/Repositories/CompanyRepository.cs`, `HousingRepository.cs`, `OfficeRepository.cs`, `SchoolRepository.cs`
- `Genelife.UnitTests/Map/MapBootstrapTests.cs`

## Acceptance

- Starting a fresh world creates a generated map before any entity is created.
- Every initial property, office and school has a placement.
- Initial stock quantities are preserved by the bootstrap.
- Construction reserves before work starts and releases on cancellation.
- A lot shortage produces a clear startup/command error, not an unplaced building.
- Existing tests either place buildings through the service or explicitly opt out of map assertions.

## Testing

- Fresh-world initialization places the expected stock.
- Exhausted residential zone produces a bootstrap error.
- HQ office placement test.
- Construction reserve/complete/cancel tests.
- Double-bootstrap idempotency test.
- Debug reconciliation test after bootstrap.

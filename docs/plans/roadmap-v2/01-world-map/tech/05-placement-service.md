# M05 — Placement service

**Phase:** 3 — Placement
**Depends:** M03, M04
**Estimate:** 2–3 sessions
**Status:** Planned (V2)

## Goal

Provide one validated path for every building placement, so no repository, system or admin command can create a building without a footprint, an entry and an occupancy reservation.

## Context

Buildings are created in many places today:

- `HousingBootstrap` creates the initial stock.
- `HousingDevelopmentSystem` creates completed construction.
- `HousingRepository`, `OfficeRepository`, `CompanyRepository` and `SchoolRepository` create admin-managed buildings.
- `SimulationEngine.ExecuteAdminCommand` routes through those repositories.
- Tests create buildings directly with `world.Create`.

If each caller chooses coordinates, placement rules will diverge. The service is the single writer for `WorldMap` occupancy and the single source of `Placement` validation.

## Design

### Request and result

```csharp
public enum PlacementError
{
    None = 0,
    OutOfBounds,
    InvalidFootprint,
    Occupied,
    WrongZone,
    NoRoadAccess,
    EntryBlocked,
    NoAvailableLot,
    DuplicateNetworkId,
    LotReserved
}

public readonly record struct PlacementRequest(
    NetworkId BuildingId,
    BuildingKind Kind,
    Footprint Footprint,
    string District,
    TileCoord? DesiredAnchor = null,
    bool ReserveLot = false);

public readonly record struct PlacementResult(
    bool Success,
    Placement Placement,
    PlacementError Error,
    string? Message,
    LotId? Lot);
```

### Service API

```csharp
public sealed class BuildingPlacementService
{
    public PlacementResult TryPlace(PlacementRequest request);
    public PlacementResult PlaceInZone(BuildingKind kind, Footprint footprint, string district);
    public bool Release(NetworkId buildingId);
    public bool ReserveLot(LotId lot, NetworkId buildingId);
    public void ReleaseLot(LotId lot);
    public IReadOnlyList<PlacementIssue> Validate(World world);
}
```

Rules:

- `TryPlace` with `DesiredAnchor` validates the exact anchor.
- `PlaceInZone` chooses the first compatible free lot in stable lot-id order.
- The footprint must fit inside a single lot. V2 does not support buildings spanning multiple lots.
- The entry is chosen deterministically from the footprint edge adjacent to a road, unless explicitly supplied.
- A construction project reserves its lot when it starts and records the lot on the project.
- A failed placement returns a structured error; it never leaves a partially reserved footprint.

### Validation order

1. Map bounds.
2. Footprint validity.
3. Duplicate network id.
4. Zone compatibility.
5. Lot availability and reservation.
6. Footprint overlap.
7. Road adjacency.
8. Entry walkability.

The order is stable so error messages are deterministic.

### Apply-phase integration

`TryPlace`/`Release` are called from:

- command drains at tick start;
- bootstrap before the first tick;
- construction completion/cancellation;
- restore backfill.

The service updates `WorldMap` and attaches/replaces `Placement` on the building entity. It emits:

- `BuildingPlaced(NetworkId, Kind, X, Y, Width, Height, EntryX, EntryY)`
- `BuildingPlacementRejected(NetworkId, Error, Message)` for admin commands only.

No HTTP endpoint calls the service directly.

### District and zone

`District` remains the domain label used by housing and economy. Placement maps it to a `MapZone` through a configured mapping; an unknown district is rejected rather than silently placed in a mixed zone. Existing districts (`Central`, etc.) are seeded in `MapGenerationOptions`.

## Tasks

- [ ] Add `PlacementError`, `PlacementRequest`, `PlacementResult` and `PlacementIssue`.
- [ ] Implement `BuildingPlacementService` with validation and deterministic lot choice.
- [ ] Implement deterministic entry selection from footprint edges.
- [ ] Reserve/release lots for construction projects.
- [ ] Attach `Placement` to the building entity and update `WorldMap`.
- [ ] Add structured events for placed/rejected buildings.
- [ ] Route `HousingBootstrap` and repositories through the service.
- [ ] Add tests for every error code and the validation order.
- [ ] Add a test that a failed placement leaves no reserved tiles or lot.

## Files

- new `Genelife/Map/BuildingPlacementService.cs`, `PlacementRequest.cs`, `PlacementResult.cs`, `PlacementError.cs`, `PlacementIssue.cs`
- `Genelife/Map/WorldMap.cs`
- `Genelife/Housing/HousingBootstrap.cs`
- `Genelife.API/Repositories/*.cs` (housing, office, company, school)
- `Genelife.Messages/Events/BuildingPlaced.cs`
- `Genelife.UnitTests/Map/PlacementServiceTests.cs`

## Acceptance

- Every successful placement reserves exactly its footprint and no adjacent tiles.
- Overlapping, out-of-bounds, wrong-zone and no-access placements are rejected with specific errors.
- Entry selection is deterministic for the same footprint and lot.
- A failed placement changes neither occupancy nor lot reservation.
- Construction can reserve a lot before completion and release it on cancellation.
- All building repositories use the service; no direct `world.Create(new Placement(...))` exists outside bootstrap and tests.

## Testing

- Error-code matrix for each validation rule.
- Stable lot selection test.
- Entry selection test with multiple road-adjacent edges.
- Reservation/commit/release test.
- Failed-placement rollback test.
- Repository integration test that a created building appears in `WorldMap`.

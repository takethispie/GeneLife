# M07 — Map admin commands

**Phase:** 3 — Placement
**Depends:** M05, M06
**Estimate:** 1–2 sessions
**Status:** Planned (V2)

## Goal

Let operators place or correct a building through the same validated command path as every other mutation, with structured errors and no direct world access from HTTP.

## Context

`AdminCommand` already carries a `CommandId` and `IssuedBy`, is drained at tick start and produces a `CommandResult`. Existing create commands (`CreatePropertyCommand`, `CreateOfficeCommand`, `CreateSchoolCommand`) have no spatial fields.

V2 should extend those commands rather than introduce a second mutation system.

## Design

### Command changes

Add optional placement fields to existing create commands:

```csharp
public sealed record CreatePropertyCommand(
    Guid CommandId,
    string IssuedBy,
    ulong OwnerId,
    string Name,
    string District,
    int Units,
    HousingTier Tier,
    Money? MonthlyRent,
    float Condition,
    float Neighbourhood,
    int? AnchorX = null,
    int? AnchorY = null,
    int? FootprintWidth = null,
    int? FootprintHeight = null,
    int? EntryX = null,
    int? EntryY = null) : AdminCommand(CommandId, IssuedBy);
```

Apply the same optional fields to office and school creation. Add a dedicated command for repairing a missing or migrated placement:

```csharp
public sealed record PlaceBuildingCommand(
    Guid CommandId,
    string IssuedBy,
    ulong BuildingId,
    int X,
    int Y,
    int Width,
    int Height,
    int EntryX,
    int EntryY) : AdminCommand(CommandId, IssuedBy);
```

A relocation command is deliberately out of scope for V2. Moving an occupied building would have to update residents, employees, students and path caches; that is a separate feature.

### Execution

Inside `SimulationEngine.ExecuteAdminCommand`:

1. resolve the building entity by `NetworkId`;
2. build a `PlacementRequest`;
3. call `BuildingPlacementService.TryPlace`;
4. on success attach `Placement`, update `WorldMap`, emit `BuildingPlaced`;
5. on failure throw a typed validation exception that becomes a failed `CommandResult` with an error code and message.

The command result payload should include:

```json
{
  "buildingId": 42,
  "placement": {
    "x": -10,
    "y": 4,
    "width": 6,
    "height": 4,
    "entryX": -8,
    "entryY": 8
  }
}
```

Failure results include `error` (`Occupied`, `OutOfBounds`, etc.) and a human-readable `message`.

### Contracts

| Method | Route | Auth | Purpose |
|---|---|---|---|
| POST | `/api/map/buildings/{id}/place` | Admin | place an existing building |
| GET | `/api/map/buildings` | Player | list buildings and placements |
| GET | `/api/map/buildings/{id}` | Player | one building and its occupancy |
| GET | `/api/map/validate` | Admin | current reconstruction issues |
| POST | `/api/properties` | Admin | existing create, now with optional placement |
| POST | `/api/offices` | Admin | existing create, now with optional placement |
| POST | `/api/schools` | Admin | existing create, now with optional placement |

All mutations enqueue a command and return `202 Accepted` with the command id.

## Tasks

- [ ] Extend property, office and school create commands with optional placement fields.
- [ ] Add `PlaceBuildingCommand`.
- [ ] Route command execution through `BuildingPlacementService`.
- [ ] Add structured error codes to `CommandResult` payloads.
- [ ] Add `/api/map/buildings/{id}/place` and map validation endpoints.
- [ ] Add command idempotency tests.
- [ ] Add an authorization test for admin-only placement.
- [ ] Document the placement payload in the map API contract.

## Files

- `Genelife.API/Commands/AdminCommand.cs`
- `Genelife.API/Services/SimulationEngine.cs` (`ExecuteAdminCommand`)
- new `Genelife.API/Endpoints/Wolverine/MapEndpoints.cs`
- `Genelife.API/DTOs/MapStatus.cs` (introduced in M09)
- `Genelife.API/Repositories/HousingRepository.cs`, `OfficeRepository.cs`, `SchoolRepository.cs`
- `Genelife.UnitTests/Map/MapCommandTests.cs`

## Acceptance

- An admin can place a building at a requested anchor and receives the resulting footprint.
- An invalid placement returns the specific error and does not mutate the world.
- Duplicate `CommandId` values are applied once.
- Non-admin tokens receive `403` on placement.
- A successful placement appears in the map read model and `WorldMap`.
- All mutations are applied at the next tick, never on the HTTP thread.

## Testing

- Command idempotency.
- Success and failure result payloads.
- Authorization test.
- Placement of each building kind.
- Endpoint integration test with admin token.
- World-map consistency after command application.

# L03 — Building relocation and commute animation

**Phase:** 3 — World
**Depends:** L01, V2 placement, V1 housing/employment, feature 03 service links
**Estimate:** 2–3 sessions
**Status:** Done (V5 feature 05)

## Goal

Close the two deferred V2 world items that affect a live town: relocating an occupied building and enabling animated commutes by default.

## Context

The admin map can inspect and place buildings but cannot relocate an occupied one. Relocation must update residents, employees, students, path caches, service links and map version. `CommuteMode.Animate` is opt-in and needs a pathfinding-budget measurement before it can default on.

V5 adds player homes, utilities and social venues, so relocation must also preserve leases, meters and venue presence.

## Design

### Relocation command

- `RelocateBuildingCommand` with building network id and destination tile/footprint.
- Validate the destination against map bounds, occupancy, roads and reserved zones.
- Reject if the destination cannot hold the footprint or breaks a service link.
- Compute the move plan before mutating anything.

### Apply phase

1. Update `Placement` and the map occupancy.
2. Move residents' home entry and spawn point.
3. Move employees' workplace entry and commute route.
4. Move students' school entry.
5. Update `HousingRegistry` paths and unit references.
6. Update utility service links and venue presence.
7. Invalidate path caches and bump the map version.
8. Emit `BuildingRelocated` and map delta events.

### Safety

- A relocation that would leave a resident or employee with an invalid path is rejected.
- A relocation that would disconnect a utility service is rejected or requires a reconnect plan.
- A building cannot be relocated during an active transaction or a player move that depends on it.
- The admin action is confirmed and audited.

### Commute animation

- Measure the pathfinding and movement budget with `CommuteMode.Animate` off and on.
- Enable it by default only if the 2,000-sim decade stays inside the tick budget.
- If the budget is exceeded, keep it opt-in and record the measured reason.
- Add a visual acceptance check for commute routes and arrival.

## Tasks

- [x] Add `RelocateBuildingCommand` and the placement validation.
- [x] Implement the move plan and apply phase.
- [x] Update residents, employees, students, housing, service links and venues.
- [x] Invalidate path caches and bump the map version.
- [x] Add `BuildingRelocated` and map delta events.
- [x] Add the admin relocation form and confirmation.
- [x] Measure the commute-animation budget.
- [x] Enable `CommuteMode.Animate` by default or record why not.

## Resolution

Occupant state is entity-referenced, so homes, workplaces, schools, meters and
venue presence move with the building rather than being recreated. Pending
`MoveTarget` destinations at the old entry are retargeted, `MovementIndex` is
rebuilt on the new map version, and the command is admin-only. The animated
commute budget measured 6.28 ms p95 against 6.42 ms for abstract on the 2,000-sim
year, so `CommuteMode.Animate` is default-on.

Relocation releases a lot reservation held by the moving building, both for its
old and new bounds, so a building moving within its own reserved lot does not
leak the reservation. `CanRelocate` treats the building's own reserved tiles as
available and rejects only reservations held by another building.

### Tests

- `BuildingRelocationTests.Relocating_within_a_reserved_lot_releases_the_reservation`
  covers the in-lot move and asserts `ReservedTileCount` returns to zero.
- The remaining relocation tests cover residents, employees, students, housing
  paths, service links, map version and path-cache invalidation.

## Files

- new `Genelife/Map/BuildingRelocationService.cs`
- `Genelife/Map/WorldMap.cs` (`CanRelocate`, `Relocate`, lot release)
- `Genelife.API/Endpoints/Wolverine/MapEndpoints.cs`
- `Genelife.API/Commands/AdminCommand.cs`
- `Genelife.Web/Pages/Map.razor`
- `Genelife/Systems/MovementSystem.cs`
- `Genelife/Systems/PathfindingSystem.cs`
- `Genelife.Domain.Tests/Map/BuildingRelocationTests.cs`

## Acceptance

- An occupied building can be relocated only to a valid destination.
- Residents, employees and students keep valid paths after the move.
- Housing, utility links and venue presence remain consistent.
- Path caches are invalidated and the map version advances.
- The admin action is confirmed and audited.
- `CommuteMode.Animate` is default-on with a measured budget or documented as opt-in.
- No occupant, employee or student is orphaned.

## Testing

- Relocate a residential building with residents and assert homes/paths.
- Relocate an office with employees and assert workplaces.
- Relocate a school with students.
- Attempt an invalid destination and assert rejection.
- Disconnect a utility link and assert rejection.
- Measure the 2,000-sim commute-animation budget.

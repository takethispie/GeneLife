# P06 — Arrival interactions and building entry

**Phase:** 3 — Movement
**Depends:** P04, M02
**Estimate:** 1–2 sessions
**Status:** Planned (V2)

## Goal

Make arrival at a building entry meaningful: proximity is validated on the server, the correct action starts, and the player cannot use a building from across the map.

## Context

`UseCommand` and `InteractCommand` exist but are empty placeholders. `ActionType` already supports sleeping, showering, eating, using the toilet, working and idling. Buildings have a `Placement.Entry` after M02. There is no proximity check or interaction range.

## Design

### Entry as the interaction point

A sim interacts with a building by standing on or adjacent to its entry tile:

```csharp
public static class BuildingAccess
{
    public static bool IsAtEntry(TilePosition position, Placement placement) =>
        Chebyshev(position.Tile, placement.Entry) <= 1;

    public static bool TryResolve(World world, WorldMap map, TileCoord tile, out Entity building);
}
```

The exact range is configurable; `1` means the entry tile or one of its eight neighbours.

### Commands

Extend the command set:

```csharp
public sealed record UseBuildingCommand : SimCommand
{
    public NetworkId BuildingNetworkId { get; init; }
}
```

`MoveCommand` gains a target tile. `InteractCommand` remains for sim-to-sim interactions in a later feature.

At apply time:

1. resolve the sim and building;
2. verify ownership and alive state;
3. verify the sim is within range of the building entry;
4. verify the building kind matches the requested use;
5. set `CurrentAction` and any building-specific state;
6. emit `BuildingUsed` or a rejection.

### Use mapping

| Building kind | Action / effect |
|---|---|
| Residential | Sleep, shower, eat, use toilet, subject to consumption state |
| Office | Work, only when employed at that office and on shift |
| School | Study, only when enrolled and on a school day |
| Store | Shop, future consumption integration |
| Civic | Shelter or service, future extension |

V2 does not add an interior map. The sim occupies the entry or an adjacent tile while the action runs. Interiors can be added later without changing the entry contract.

### Arrival

- `MovementSystem` emits `SimArrived` when the final tile is reached.
- If the movement target was a building entry, the event includes the building id.
- The client may send `UseBuilding` after arrival, or the server may auto-start the default action for NPCs.
- A player-controlled sim does not auto-use; the player decides.

### Rejections

- `NotInRange`
- `WrongBuildingKind`
- `NotEmployedHere`
- `NotEnrolledHere`
- `ActionBusy`
- `SimDead`

These are server-side checks, not client-side hints.

## Tasks

- [ ] Add `UseBuildingCommand` and target-tile `MoveCommand`.
- [ ] Implement `BuildingAccess` proximity and tile resolution.
- [ ] Implement kind-specific use validation.
- [ ] Start the appropriate action and emit `BuildingUsed`.
- [ ] Add `SimArrived` with an optional building id.
- [ ] Add NPC default-use rules where appropriate.
- [ ] Add rejection codes and tests.
- [ ] Expose the current building/entry in the admin sim status.

## Files

- new `Genelife/Map/BuildingAccess.cs`
- `Genelife.API/Commands/SimCommand.cs`, `AdminCommand.cs`
- `Genelife.API/Commands/PlayerIntentDispatcher.cs`
- `Genelife/Systems/MovementSystem.cs`
- `Genelife/Systems/ActionSystem.cs`
- `Genelife.Messages/Events/BuildingUsed.cs`, `SimArrived.cs`
- `Genelife.UnitTests/Systems/BuildingInteractionTests.cs`

## Acceptance

- A sim at or adjacent to an entry can use the building.
- A sim several tiles away is rejected with `NotInRange`.
- Using an office requires employment at that office.
- Using a school requires enrollment.
- A player-controlled sim never auto-starts a building action.
- Arrival at an entry emits an event with the building id.
- No interior map or pathfinding-through-buildings is required.

## Testing

- Proximity boundary tests.
- Kind/employment/enrollment authorization.
- Arrival event payload.
- Action start/clear.
- Player auto-use exclusion.
- Dead/busy rejection.

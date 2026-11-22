# Map generation and density

A map cannot be a blank plane where buildings are scattered by whoever creates them first. It needs a repeatable structure: land, roads, blocks, lots and zones. That structure is what makes placement, commute and future growth believable.

## Deterministic generation

The map is generated from a seed and a map version. The same pair must produce the same terrain, roads, lots and initial building positions. This matters for:

- save/restore and replay;
- reproducing a bug from a scenario seed;
- comparing balance runs;
- giving clients a stable layout to cache;
- proving that placement is not dependent on wall-clock time or hash ordering.

Generation uses the existing seeded random approach, with named streams for map generation, placement and fallback spawn selection. Stable ordering is part of the contract.

## Roads, lots and zones

The generator produces a usable structure before any building exists:

- **Terrain** — land and blocked areas such as water or reserved ground.
- **Roads** — the network that gives buildings access.
- **Blocks** — the spaces between roads.
- **Lots** — parcels inside blocks that can hold a footprint.
- **Zones** — residential, commercial, education, civic and mixed areas.

A building is placed into a compatible lot. The lot records its capacity and remaining footprint area, so the map can answer "is there room for another property here?" instead of failing late.

## Density is a design parameter

Density is not only visual. It controls how many buildings and households a district can hold, how far residents travel and how crowded the map feels. The generator should carry explicit density targets per zone rather than filling every tile.

Useful controls:

- maximum built area per zone;
- minimum spacing between building kinds;
- number of civic and education plots;
- reserved land for future construction;
- fallback behaviour when a district has no lot left.

## Growth uses the same rules

New housing from `HousingDevelopmentSystem` and new offices or schools created through admin commands must use the same lot and placement path. Construction should reserve a lot when it starts, not discover at completion that there is nowhere to put the building.

Cancellation releases the reservation. Demolition releases the footprint. The map should remain consistent through both.

## What goes wrong without this

- Buildings overlap or sit outside the map.
- A construction project completes with no valid location.
- Two systems choose the same corner for different buildings.
- A restore produces a different layout from the original run.
- The client and server disagree about what is on a tile.

Map generation is therefore not cosmetic setup. It is the deterministic source of valid places for everything that follows.

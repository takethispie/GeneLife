# Why a spatial world

V1 simulates a population through money, housing and life stages, but it has no geography. A property, an office and a school are distinguished only by name and district string. Every sim stands at `(0,0)`. The simulation is internally consistent, but it cannot answer basic spatial questions: where is the office, how far is the school, who is nearby, or what happens when a player walks into a building.

## What space adds

- **A common reference.** Every system can point at the same place: this building is at this footprint, this sim is on this tile, this office has this entrance.
- **Collision.** Buildings stop being abstractions with an address and become obstacles that movement must respect.
- **Distance and access.** Commute length, school choice, service access and future neighbourhood effects need a measurable distance rather than a label.
- **Player presence.** A player-controlled sim is meaningful only if its position can be trusted, validated and replicated.
- **Operable geography.** An admin can see the map, spot an overlapping building and place a new one without guessing.

## Why this is backend work first

The map is not a rendering surface. It is a domain contract. If buildings can be created without a position, or if two systems disagree about where a building is, a map view will only make the inconsistency visible. The backend must first guarantee:

1. one canonical coordinate system;
2. one placement per building;
3. one derived occupancy index;
4. one path that validates and reserves a footprint;
5. deterministic generation and persistence.

## The map as a shared contract

A map is not just a grid of tiles. It is the agreement between buildings, movement, persistence and replication:

- buildings claim footprints;
- movement reads the same footprints as blocked tiles;
- snapshots and map deltas derive from the same placements;
- restore rebuilds the same occupancy from the same components.

When these agree, the map is a reliable part of the simulation. When they do not, every other feature inherits the inconsistency.

## What the admin should be able to see

- the generated map and its bounds;
- every building footprint and entrance;
- occupied, free and blocked tiles;
- buildings without a placement;
- overlapping or out-of-bounds placements;
- live player positions;
- structured placement errors when a new building cannot be placed.

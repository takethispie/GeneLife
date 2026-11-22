# Buildings have a place

A building is more than a name and a district. It occupies ground, has an entrance and constrains what can happen around it. The backend should model that once, on the building entity, and let everything else inherit it.

## The building owns its placement

A property is not a collection of independently placed apartments. It is one building with one footprint. Its units share the building's position. The same rule applies to an office and a school.

This keeps the model honest:

- one `Placement` per building entity;
- `HousingUnit` inherits from `Property`;
- `Employee` inherits from `Office`;
- `Enrollment` inherits from `School`.

If every unit copied coordinates, the copies would drift. If every employee stored a workplace position, a move or a correction would update some records and not others.

## Footprint, anchor and entry

Three pieces of information are enough for most backend behaviour:

- **Anchor** — the building's reference tile.
- **Footprint** — the width and height it occupies.
- **Entry** — the tile a sim or player must reach to use the building.

The entry matters because movement and interactions should not require walking into an arbitrary interior tile. Sleep, work, school and shopping all resolve to a doorway or a service point.

## Zone and district are not the same as position

A district is a semantic label used by housing, economy and administration. It is useful for grouping and policy, but it cannot answer "is this tile free?" A placement answers that.

Both can exist:

- `District` says which part of the world the building belongs to.
- `Placement` says exactly where it stands.

## Buildings are static; sims are dynamic

Buildings change rarely and should not be part of every position delta. Sims move constantly and need cheap updates. Treating them differently keeps both the tick loop and the replication path small.

- placement changes emit map events;
- sim movement emits position changes;
- map snapshots are sent on connect and after building changes;
- sim positions travel in the normal world snapshot and delta.

## Validation is part of the product

A building cannot simply be dropped anywhere. Placement must check bounds, overlap, zoning, road access and entry walkability. A rejected placement should explain why, not silently leave a building at the origin.

The admin experience is therefore part of the feature: inspect the map, attempt a placement, read a structured error, adjust and retry.

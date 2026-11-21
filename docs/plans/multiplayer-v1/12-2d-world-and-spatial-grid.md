# Step 12 — 2D world and spatial grid

**Depends:** 03, 06
**Goal:** Make the world truly 2D and enable spatial queries and AOI filtering.

## Tasks

- [ ] Replace `Location.Vector3` with `Vector2` (or grid coordinates).
- [ ] Add a spatial grid (cell size TBD) owned by the tick thread.
- [ ] Update the grid on spawn, move, and destroy.
- [ ] Add neighbor/radius queries for AI (relationships, attraction).
- [ ] Filter snapshots per player by AOI (radius or cell neighborhood).
- [ ] Define world bounds and spawn rules.
- [ ] Migrate tests currently asserting `Vector3`.

## Files likely touched

- `Genelife/Components/Gen/Location.cs`
- new `Genelife/Systems/SpatialIndex.cs`
- `Genelife.Api/Snapshots/*`

## Acceptance

- No `Vector3` remains in simulation code.
- Neighbor/AOI queries are correct.
- Grid stays consistent through spawn/move/destroy.

## Testing

- Neighbor inclusion/exclusion; AOI filtering; entity churn.

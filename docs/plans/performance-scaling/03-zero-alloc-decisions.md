# Step 03 — Zero-allocation DecisionSystem

**Depends:** 02
**Goal:** Remove per-entity allocations from the decision hot path.

**Status:** Done

## Context

`GetLowestNeed` allocates a 4-tuple array and uses `OrderBy().First()` per entity per tick.

## Tasks

- [x] Replace the array/LINQ with four explicit comparisons.
- [x] Return a small struct (need kind + duration); no heap allocation.
- [x] Keep behavior identical (lowest need wins; threshold unchanged).
- [x] Add an allocation assertion around `Update`.

## Files likely touched

- `Genelife/Systems/DecisionSystem.cs`
- `Genelife.UnitTests/Systems/DecisionSystemTests.cs`

## Acceptance

- `DecisionSystem.Update` allocates ~0 bytes per tick.
- All existing decision tests pass unchanged.

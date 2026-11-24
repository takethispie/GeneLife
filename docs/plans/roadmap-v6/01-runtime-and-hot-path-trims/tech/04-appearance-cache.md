# Appearance Cache

## Context

`BuildSimSnapshots` calls `AppearanceMapper.TryDerive` for every sim every tick (`Genelife.API/Services/SimulationEngine.cs:5854`). Appearance is derived deterministically from traits, age band and sex, so it only changes when those inputs change. Deriving it every tick is repeated work.

## Design

Cache the derived appearance per sim and invalidate on input change:

- Key the cache by sim entity plus an appearance version.
- Invalidate when traits change, the age band changes or the appearance version changes.
- Read the cached descriptor in `BuildSimSnapshots`.

The cache lives in the engine and is rebuilt on restore. It is not persisted.

## Tasks

- [ ] Add an appearance cache keyed by entity with a version/age-band stamp.
- [ ] Invalidate on trait change, age-band change and appearance-version change.
- [ ] Read the cache in `BuildSimSnapshots` instead of calling `TryDerive` unconditionally.
- [ ] Rebuild the cache on world restore.
- [ ] Run the 2k `LoadHarness` and record allocation per tick.

## Files

- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.API/Appearance/AppearanceMapper.cs` (read path only)

## Contracts

- Derived appearance is identical for unchanged inputs.
- The cache is not persisted and does not affect the world schema.

## Acceptance

- Appearance derivation runs only for sims whose inputs changed.
- Snapshot output is identical to the uncached path.
- The 2k run shows lower per-tick allocation.
- Restore rebuilds the cache and produces the same snapshots.

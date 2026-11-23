# T06 — Trait persistence and migration

**Phase:** 6 — Durability
**Depends:** T01–T05
**Estimate:** 2 sessions
**Status:** Done

## Goal

Persist traits exactly, migrate schema 5 saves to schema 6 deterministically, and guarantee that a second restore produces the same traits.

## Context

`PersistedWorldState.CurrentSchemaVersion` is 5. It already persists economy/housing options and `BalanceBackfilled`. `PersistedSim` has no trait fields.

V2 and the 2026-11 balance pass established the migration pattern: add fields, backfill deterministically, keep older fields, and test save/restore plus a legacy fixture.

## Design

### Schema 6

`PersistedWorldState.CurrentSchemaVersion = 6`.

`PersistedSim` gains:

```csharp
public int EyeColor { get; set; }
public int HairColor { get; set; }
public int Handedness { get; set; }
public int Morphotype { get; set; }
public int Aptitude { get; set; }
public int HeightPotential { get; set; }
public int Sex { get; set; }
public int Temperament { get; set; }
public bool TraitsMigrated { get; set; }
```

`PersistedWorldState` gains:

```csharp
public Genelife.Family.GeneticsOptions? GeneticsOptions { get; set; }
public string AttractionModelVersion { get; set; } = "v4-attraction-2";
```

### Capture

- Write each trait as its integer enum value.
- Write `TraitsMigrated = true` for sims created after V4.
- Persist `GeneticsOptions` like the existing economy/housing options.

### Restore

- If `state.SchemaVersion < 6` or `!sim.TraitsMigrated`, generate traits deterministically:
  `TraitsGenerator.Create(state.RandomSeed, sim.NetworkId, TraitSource.Migration, options)`.
- Set `sim.TraitsMigrated = true` in memory so the next save is stable.
- Add the `Traits` component to the sim.
- Validate every enum; an undefined value is a hard restore error that names the network id.
- Preserve the existing V5 fields and the `BalanceBackfilled` behaviour.

### Double restore

- The migration must not depend on the current tick or on entity creation order.
- Restoring the same V5 fixture twice must produce identical trait values.
- Saving the migrated V6 state and restoring it again must not change traits.

### Snapshot size

- Eight `int` fields plus a bool per sim. At 2,000 sims this is under 100 KB before compression.
- If size becomes a concern, compress enums into a single `ulong` bitfield in a later schema; do not do it in the first migration.

## Tasks

- [ ] Bump `CurrentSchemaVersion` to 6.
- [ ] Add trait fields and `TraitsMigrated` to `PersistedSim`.
- [ ] Add `GeneticsOptions` and `AttractionModelVersion` to `PersistedWorldState`.
- [ ] Capture traits in `SimulationEngine.CaptureState`.
- [ ] Restore traits and migrate missing ones deterministically.
- [ ] Validate enum values and fail loudly on corruption.
- [ ] Add a checked-in V5 fixture with no trait fields.
- [ ] Add save/restore, double-restore and V5 migration tests.

## Files

- `Genelife.API/Persistence/PersistedWorldState.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.UnitTests/Persistence/TraitPersistenceTests.cs`
- `Genelife.UnitTests/Fixtures/v5-no-traits.json` (or an equivalent fixture helper)
- `docs/plans/roadmap-v4/05-admin-observability-and-hardening/tech/06-persistence-restore-and-migration.md`

## Acceptance

- A V6 save contains one trait row per sim.
- Restoring a V6 save reproduces every trait exactly.
- A V5 fixture gets a stable, deterministic trait set and `TraitsMigrated = true`.
- Restoring the migrated state again and saving does not change any trait.
- An undefined enum value fails restore with a descriptive error.
- Existing economy/housing option persistence still round-trips.

## Testing

- Save/restore a populated world and compare every trait.
- Migrate a V5 fixture, capture, restore, compare.
- Migrate twice and compare.
- Corrupt one enum and assert the error.
- Assert the snapshot remains within the size budget at 2,000 sims.
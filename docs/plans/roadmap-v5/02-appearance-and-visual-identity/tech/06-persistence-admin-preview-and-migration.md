# A06 — Persistence, admin preview and migration

**Phase:** 6 — Durability
**Depends:** A01–A05, schema 12
**Estimate:** 2 sessions
**Status:** Planned (V5)

## Goal

Persist the appearance genes and version, migrate legacy saves deterministically, and give operators an appearance preview without exposing traits to players.

## Context

Schema 12 adds player state. Schema 13 adds `SkinTone`, `FaceShape` and appearance metadata. The admin already has a Family page with trait values; it needs a visual preview and a family comparison.

The client composes appearance from the descriptor, so the server must persist the genes and version, not a rendered image.

## Design

### Schema 13

`PersistedWorldState.CurrentSchemaVersion` moves from 12 to 13.

`PersistedSim` gains:

```csharp
public byte SkinTone { get; set; }
public byte FaceShape { get; set; }
public byte AppearanceVersion { get; set; }
public bool AppearanceMigrated { get; set; }
```

- `SkinTone` and `FaceShape` are inherited traits and must round-trip.
- `AppearanceVersion` records the derivation contract used at save time.
- `AppearanceMigrated` prevents reapplying the legacy backfill.

### Migration

- Schema 11 and 12 sims without appearance genes get deterministic values from world seed, `NetworkId` and appearance version.
- Existing traits are not changed.
- A second restore produces identical genes and descriptors.
- A save with a future appearance version fails loudly rather than silently reinterpreting it.

### Admin preview

`/appearance` supports:

- sim search and selection;
- descriptor and layer list;
- a rendered portrait from the shared layer contract;
- family comparison (parents, children, siblings);
- appearance version and cache state;
- a diagnostic for missing atlas parts.

The preview is admin-only. Player routes receive only their own descriptor and public appearance data.

### Events and projections

- `AppearanceDerived`, `AppearanceMigrated`, `AppearanceVersionChanged`.
- Projections are idempotent and rebuildable.
- Appearance is not a separate authoritative document; the sim document carries the genes and version.

## Tasks

- [ ] Add the schema 13 fields and migration.
- [ ] Add deterministic backfill and the migrated flag.
- [ ] Add `AppearanceDto` to the sim read model.
- [ ] Add the `/api/appearance` admin endpoints.
- [ ] Add the `/appearance` admin page and family comparison.
- [ ] Add appearance events and projection coverage.
- [ ] Add migration, restore and double-restore tests.
- [ ] Add an HTTP smoke test for admin authorization.

## Files

- `Genelife.API/Persistence/PersistedWorldState.cs`
- `Genelife.API/Persistence/SnapshotMigrations.cs`
- new `Genelife.API/Appearance/AppearanceReadModel.cs`
- new `Genelife.API/Endpoints/AppearanceEndpoints.cs`
- new `Genelife.Web/Pages/Appearance.razor`
- `Genelife.UnitTests/Persistence/AppearancePersistenceTests.cs`

## Acceptance

- Schema 11 and 12 saves migrate to 13 with deterministic appearance genes.
- A second restore produces the same descriptor.
- Existing traits are unchanged by the migration.
- The admin preview renders the same layer contract as the browser.
- The admin can compare a family and see appearance versions.
- Player routes never expose another player's appearance genes or traits.
- Events have idempotent projections.

## Testing

- Migrate a schema 11 fixture and compare descriptors.
- Double restore and compare.
- Save/restore a family and compare parent/child descriptors.
- Rebuild projections and compare.
- Assert unauthorized `/api/appearance` returns 401/403.
- Assert the page renders with empty and seeded worlds.

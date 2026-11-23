# Appearance and Visual Identity — Summary

**Folder:** `02-appearance-and-visual-identity/`
**Status:** Planned (V5 feature 02)

**Depends on:** V4 `Traits`, trait inheritance, V3 browser renderer, `SimStatus` snapshots, schema 11
**Feeds:** player life, social and multiplayer, live operations and scale

## Objective

Make the V4 trait system visible. Every sim should have a deterministic appearance derived from inherited traits, rendered from layered atlas parts, with family resemblance and aging visible in the browser client. The player should be able to recognize their own sim, their relatives and other players without the client owning a second genetic model.

This feature does not add a full character creator, a wardrobe economy or per-sim portrait assets. It turns existing traits into a compact visual descriptor and a composable sprite.

## Baseline before V5

- `Traits` contains `EyeColor`, `HairColor`, `Handedness`, `Morphotype`, `Aptitude`, `HeightPotential`, `Sex` and `Temperament`.
- V4 explicitly deferred appearance rendering; traits are visible only in the admin family page.
- Browser sims are pooled circles tinted by local player, other player or NPC.
- The client has a texture atlas loader, pooled sprites and a sim layer.
- `SimStatus` carries position, action and basic identity, but no appearance.
- There is no skin tone, face shape, hair style, age-band visual or portrait composition.
- `PersistedWorldState` is schema 11.

## Deliverables

1. `SkinTone` and `FaceShape` added to the trait schema as append-only enum values.
2. A compact, deterministic `AppearanceDescriptor` derived from traits, age and sex.
3. Appearance inheritance and family resemblance with bounded variation.
4. Layered portrait and sprite composition from an atlas, palettes and tint.
5. Aging visuals across child, teen, adult, middle and senior bands.
6. Compact appearance fields in `SimStatus` with delta-friendly encoding.
7. Schema 13 persistence, deterministic backfill and an admin appearance preview.
8. Appearance determinism, payload and frame-budget acceptance tests.

## Architecture

```
Traits (inherited, persisted)
   │
   ├── Age / Sex / Morphotype / HeightPotential
   ▼
AppearanceDescriptor (deterministic, compact)
   │
   ├── SkinTone, FaceShape, HairColor, HairStyle, EyeColor
   ├── BodyFrame, HeightScale, AgeBand, Expression
   └── AppearanceVersion
   │
   ├──► SimStatus.Appearance (bytes) ──► browser snapshot/delta
   │                                         │
   │                                         ▼
   │                                  layered atlas composition
   │                                         │
   │                                         ▼
   │                                  pooled sim sprites
   │
   └──► Admin appearance preview and family comparison
```

Appearance is a projection of genetics plus age, not a second persisted source of truth. The descriptor is cached and invalidated by trait, age-band or appearance-version changes.

## Tech steps

| # | Step | Phase |
|---|---|---|
| 01 | [Appearance descriptor model](tech/01-appearance-descriptor-model.md) | Foundation |
| 02 | [Appearance derivation and inheritance](tech/02-appearance-derivation-and-inheritance.md) | Genetics |
| 03 | [Portrait and sprite composition](tech/03-portrait-and-sprite-composition.md) | Rendering |
| 04 | [Aging and family resemblance](tech/04-aging-and-family-resemblance.md) | Rendering |
| 05 | [Client asset pipeline and rendering](tech/05-client-asset-pipeline-and-rendering.md) | Client |
| 06 | [Persistence, admin preview and migration](tech/06-persistence-admin-preview-and-migration.md) | Durability |
| 07 | [Appearance acceptance](tech/07-appearance-acceptance.md) | Verification |

## Business reading

- [Why appearance matters](business/01-why-appearance-matters.md)
- [Fairness and representation](business/02-fairness-and-representation.md)
- [Appearance without an asset explosion](business/03-appearance-without-an-asset-explosion.md)

## Definition of done

- Every living sim has a deterministic appearance descriptor derived from traits and age.
- `SkinTone` and `FaceShape` migrate deterministically for legacy sims.
- A child's appearance resembles its parents because it inherits their traits, not because of a family portrait rule.
- The browser renders sims from layered atlas parts and palettes, not one texture per sim.
- Aging changes proportions, hair colour and posture within the same identity.
- The local player, other players and NPCs are visually distinguishable without relying on colour alone.
- `SimStatus` carries appearance in a compact, delta-friendly form and stays within the payload budget.
- The admin can preview a sim's appearance and compare a family.
- Appearance determinism, restore, payload and frame-budget tests pass.

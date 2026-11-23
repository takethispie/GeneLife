# A02 — Appearance derivation and inheritance

**Phase:** 2 — Genetics
**Depends:** A01, V4 trait generation and inheritance
**Estimate:** 2 sessions
**Status:** Planned (V5)

## Goal

Derive appearance deterministically from traits and age, and make children resemble their parents through the existing inheritance path.

## Context

V4 already generates traits for seeded sims, immigrants, player sims and newborns, and inherits them from two parents with bounded mutation. Appearance should not invent a second inheritance rule. The only new genetic fields are `SkinTone` and `FaceShape`; everything else is derived.

Legacy sims have no skin tone or face shape. They need a deterministic backfill that does not change their existing traits.

## Design

### Derivation

```csharp
public static AppearanceDescriptor Derive(
    in Traits traits,
    int age,
    byte appearanceVersion,
    ulong stableKey)
```

Inputs and rules:

| Field | Source |
|---|---|
| `SkinTone` | inherited trait |
| `FaceShape` | inherited trait |
| `HairColor` | inherited trait |
| `EyeColor` | inherited trait |
| `HairStyle` | derived from `FaceShape`, `Morphotype`, age and a stable draw |
| `BodyFrame` | derived from `Morphotype` |
| `HeightScale` | derived from `HeightPotential` and age band |
| `AgeBand` | derived from `Alive.Age` |
| `Expression` | derived from `Temperament` |

The stable draw uses `SimulationRandom.For("appearance.derive")` keyed by `stableKey` and the appearance version. It only breaks ties for derived fields; it never overrides an inherited trait.

### Inheritance

- `SkinTone` and `FaceShape` join the V4 parent-pair inheritance with the same mutation bounds as the other categorical traits.
- A child takes one parent's value per trait, then applies the bounded mutation.
- Hair colour, eye colour and the other existing traits continue to inherit exactly as in V4.
- Newborns, seeded sims, immigrants, player sims and admin sims all use the same derivation.
- A child with one known parent uses that parent plus the existing fallback rule.

### Legacy backfill

- Schema 11 sims get `SkinTone` and `FaceShape` from `SimulationRandom.For("appearance.derive")` keyed by world seed, `NetworkId` and appearance version.
- Existing traits do not change.
- A second restore produces identical values.
- The backfill is marked with `AppearanceMigrated` so it is not reapplied.

### Appearance version

- Bump `AppearanceVersion` when derivation rules or atlas contracts change.
- A version change invalidates client composition caches but does not rewrite traits.
- Persist the version used for the last derivation.

## Tasks

- [ ] Add `SkinTone` and `FaceShape` to trait generation.
- [ ] Add both fields to parent-pair inheritance and mutation.
- [ ] Implement `AppearanceDerivation.Derive` with named RNG streams.
- [ ] Add the legacy backfill and `AppearanceMigrated` flag.
- [ ] Add a deterministic stable key for seeded, immigrant, newborn and player sims.
- [ ] Add family resemblance fixtures.
- [ ] Add replay and double-restore tests.
- [ ] Document the derivation table in the admin read model.

## Files

- new `Genelife/Appearance/AppearanceDerivation.cs`
- `Genelife/Family/TraitsGenerator.cs`
- `Genelife/Family/TraitsInheritance.cs`
- `Genelife/Family/BirthSystem.cs`
- `Genelife.API/Persistence/SnapshotMigrations.cs`
- `Genelife.UnitTests/Appearance/AppearanceDerivationTests.cs`

## Acceptance

- Every creation path produces a valid descriptor.
- A child inherits `SkinTone` and `FaceShape` from a parent with bounded mutation.
- The same seed, traits, age and version produce the same descriptor.
- A version bump changes derived fields but not inherited traits.
- A schema 11 save backfills deterministically and a second restore is identical.
- No appearance derivation reads the whole population or uses `Random.Shared`.

## Testing

- Derive 10,000 descriptors and assert valid values and plausible distribution.
- Inherit with mutation 0 and assert each trait equals one parent.
- Repeat with mutation and assert the observed rate is within tolerance.
- Compare parent and child descriptors for family resemblance.
- Restore a schema 11 fixture twice and compare.
- Assert derivation never mutates `Traits`.

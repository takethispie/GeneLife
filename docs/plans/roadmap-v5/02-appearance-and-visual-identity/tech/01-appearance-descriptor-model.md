# A01 — Appearance descriptor model

**Phase:** 1 — Foundation
**Depends:** V4 `Traits`
**Estimate:** 1–2 sessions
**Status:** Planned (V5)

## Goal

Define the compact, deterministic appearance model that the simulation, the wire contract, the browser renderer and the admin preview all share.

## Context

V4 traits describe genetics but not enough to draw a person. There is no skin tone, face shape or hair style, and no compact way to send appearance to the client. Sending the full trait set to every player would leak admin-only data and add payload weight to every snapshot.

The descriptor must be small enough for `SimStatus`, stable across restore and derived only from server-owned state.

## Design

### New trait enums

```csharp
namespace Genelife.Components.Gen;

public enum SkinTone : byte { Porcelain, Fair, Olive, Tan, Brown, Deep }
public enum FaceShape : byte { Oval, Round, Square, Heart, Long }
```

Add both to `Traits` as append-only fields. Existing enum values never move.

### Appearance enums

```csharp
namespace Genelife.Appearance;

public enum HairStyle : byte { Short, Cropped, Wavy, Straight, Curly, Braided, Bald }
public enum BodyFrame : byte { Slim, Average, Sturdy }
public enum AgeBand : byte { Child, Teen, Adult, Middle, Senior }
public enum Expression : byte { Calm, Warm, Serious, Alert }
```

`BodyFrame` is derived from `Morphotype`; `Expression` is derived from `Temperament`. They are not new genetic fields.

### Descriptor

```csharp
public readonly record struct AppearanceDescriptor(
    SkinTone SkinTone,
    FaceShape FaceShape,
    HairColor HairColor,
    HairStyle HairStyle,
    EyeColor EyeColor,
    BodyFrame BodyFrame,
    byte HeightScale,
    AgeBand AgeBand,
    Expression Expression,
    byte AppearanceVersion);
```

`HeightScale` is a byte in the 0–100 range derived from `HeightPotential` and age, not a float in the hot state.

### Encoding

- Every field is a byte or a small enum.
- The wire DTO uses the same numeric values, not strings.
- `AppearanceVersion` increments when the derivation rules or the atlas contract change.
- A descriptor hash is used for client composition caching; the hash is not persisted.

### Rules

- Appearance is derived from `Traits`, `Alive.Age` and `Sex`; it is never authored independently.
- `SkinTone` and `FaceShape` are inherited like the other traits.
- `HairStyle`, `BodyFrame`, `HeightScale`, `AgeBand` and `Expression` are derived, not persisted as genes.
- Missing or undefined enum values are a validation defect.
- A sim's appearance is stable for a given trait set, age band and appearance version.

## Tasks

- [ ] Add `SkinTone` and `FaceShape` to `Traits` with validation.
- [ ] Add `HairStyle`, `BodyFrame`, `AgeBand`, `Expression` and `HeightScale`.
- [ ] Add `AppearanceDescriptor` and its compact encoding.
- [ ] Add `AppearanceValidation` for defined values and version.
- [ ] Add a descriptor hash and a test fixture factory.
- [ ] Document the append-only enum rule for persisted traits.
- [ ] Add size, equality and round-trip tests.

## Files

- new `Genelife/Components/Gen/SkinTone.cs`
- new `Genelife/Components/Gen/FaceShape.cs`
- new `Genelife/Appearance/AppearanceDescriptor.cs`
- new `Genelife/Appearance/AppearanceEnums.cs`
- new `Genelife/Appearance/AppearanceValidation.cs`
- `Genelife/Components/Gen/Traits.cs`
- `Genelife.UnitTests/Appearance/AppearanceDomainTests.cs`

## Acceptance

- `AppearanceDescriptor` is a fixed-size value with no strings.
- Every enum round-trips through its numeric value.
- Validation rejects undefined values.
- The descriptor is stable for the same traits, age and version.
- The descriptor hash changes when any visual field changes.
- No appearance field is persisted as an independent source of truth.

## Testing

- Serialize and deserialize each enum value.
- Assert the descriptor size and default value are stable.
- Assert a changed skin tone changes the hash.
- Assert an undefined enum value fails validation.
- Assert the same trait fixture always produces the same descriptor.

# T01 — Trait domain model

**Phase:** 1 — Foundation
**Depends:** V1 life stages, `AttractivenessService` schema
**Estimate:** 1–2 sessions
**Status:** Done

## Goal

Define the canonical trait value types and the single `Traits` component every V4 feature reads. The schema must be compact, validated, deterministic and mappable to the existing attraction prototype.

## Context

A sim currently has no sex, physical traits or temperament. `AttractivenessService/Attractiveness.consumption.cs` defines the fields its ML.NET model expects for two partners: eye colour, hair type, handedness, morphotype, intelligence, height potential, age, sex and behaviour propensity.

The prototype uses strings and floats. That is appropriate for a training file, not for ECS state. V4 needs a small blittable component with stable enum values.

## Design

### Enums

```csharp
namespace Genelife.Components.Gen;

public enum EyeColor : byte { Brown, Blue, Green, Grey, Hazel }
public enum HairColor : byte { Black, Brown, Blonde, Ginger, Grey }
public enum Handedness : byte { Right, Left, Ambidextrous }
public enum Morphotype : byte { Ectomorph, Mesomorph, Endomorph }
public enum Aptitude : byte { Science, Art, Craft, Social, Physical }
public enum HeightPotential : byte { Short, Average, Tall }
public enum Sex : byte { Female, Male }
public enum Temperament : byte { Peaceful, Neutral, Violent }
```

### Component

```csharp
public record struct Traits
{
    public EyeColor EyeColor;
    public HairColor HairColor;
    public Handedness Handedness;
    public Morphotype Morphotype;
    public Aptitude Aptitude;
    public HeightPotential HeightPotential;
    public Sex Sex;
    public Temperament Temperament;
}
```

### Rules

- One `Traits` component per living sim. Newborns, seeded sims, immigrants, player sims and admin-created sims all use the same component.
- No `Age` field. Age remains `Alive.Age` / `Lifecycle.AgeTicks`.
- No `Unknown` value. Missing traits are a migration/creation defect, not a valid state. Legacy saves use a `TraitsMigrated` flag.
- Enum values are append-only. Never reorder existing values because they are persisted as integers.
- `Sex` is read only by pairing and reproduction policy. No other system may use it for pay, hiring, housing or social role.
- Trait values are immutable after creation except when a newborn is generated.

### Attraction prototype mapping

The calibration adapter is a boundary type, not a dependency of the simulation:

| Prototype column | Canonical field | Mapping |
|---|---|---|
| `F/S_EyeColor` | `EyeColor` | direct enum name |
| `F/S_HairType` | `HairColor` | prototype values are colours; rename at the boundary |
| `F/S_Handedness` | `Handedness` | direct |
| `F/S_Morphotype` | `Morphotype` | direct |
| `F/S_Intelligence` | `Aptitude` | prototype domain values map to `Science`/`Art`/`Craft`/`Social`/`Physical` |
| `F/S_HeightPotential` | `HeightPotential` | prototype `Short`/`Average`/`Tall` |
| `F/S_Age` | `Alive.Age` | derived, never stored in `Traits` |
| `F/S_Sex` | `Sex` | direct |
| `F/S_BehaviorPropension` | `Temperament` | prototype `Peaceful`/`Neutral`/`Violent` |

### Validation

Add `TraitValidation.Validate(in Traits traits)`:

- every enum value is defined for its type;
- no field is a reserved future slot;
- no combination is rejected in V4a (all enum combinations are valid);
- the validator is used by creation, restore and the admin read model.

## Tasks

- [ ] Add the eight enums and `Traits` under `Genelife/Components/Gen`.
- [ ] Add `TraitValidation` with `Enum.IsDefined` checks and a descriptive error.
- [ ] Add mapping constants for the prototype column names.
- [ ] Add a small `Traits` factory for tests and fixtures.
- [ ] Decide and document that missing traits are invalid for living sims.
- [ ] Add value-size and equality tests.

## Files

- new `Genelife/Components/Gen/Traits.cs`
- new `Genelife/Family/TraitValidation.cs`
- new `Genelife/Family/TraitMapping.cs`
- `Genelife.UnitTests/Family/TraitDomainTests.cs`

## Acceptance

- `Traits` is a blittable record struct with eight byte-sized fields.
- Every enum value round-trips through its numeric value.
- `TraitValidation` rejects undefined enum values and accepts every defined combination.
- The prototype mapping is documented and covered by a test.
- No `Age` or derived runtime value is stored in `Traits`.

## Testing

- Serialize each enum value and read it back.
- Assert the component size and default value are stable.
- Assert mapping from prototype column names to canonical fields.
- Assert validation fails for an out-of-range value.
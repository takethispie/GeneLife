# H01 — Housing domain model

**Phase:** 1 — Foundation
**Depends:** E01 (`Money`), E02 (`SimulationClock`)
**Estimate:** 2 sessions
**Status:** Implemented (V1)

## Goal

Define properties, units, households and tenure as ECS components with clear ownership and one source of truth per relationship.

## Context

The world has `Location` and `SpatialIndex` (`Genelife/Systems/SpatialIndex.cs`) but no residential entities. Housing needs entities that survive persistence, can be administered, and are cheap to query per tick.

## Design

### Entities and components

```csharp
public enum HousingTier { Shelter, Basic, Comfortable, Luxury }
public enum Tenure { Dependent, Renter, Owner, Sheltered }
public enum HouseholdRole { Head, Partner, Child, Relative, Other }

public record struct Property
{
    public string Name;
    public string District;
    public Entity Owner;            // company or household; Entity.Null = municipality
    public int UnitCount;
    public float Condition;          // 0..1
    public float NeighbourhoodScore; // 0..1
    public Money MonthlyUpkeep;
    public bool IsShelter;
}

public record struct HousingUnit
{
    public Entity Property;
    public string UnitNumber;
    public HousingTier Tier;
    public int Capacity;
    public Money BaseRent;
    public float Condition;
    public bool IsVacant;
}

public record struct Household
{
    public Entity Head;
    public Entity Unit;
    public Tenure Tenure;
    public Money MonthlyRent;
    public long MoveInTick;
    public int ArrearsCycles;
    public Money RentArrears;
}

public record struct HouseholdMember
{
    public Entity Household;
    public HouseholdRole Role;
}

public record struct Residence   // derived cache for O(1) sim → home reads
{
    public Entity Household;
    public Entity Unit;
}
```

`Household` is the source of truth for tenancy. `Residence` is a derived component updated in apply phases and asserted in debug builds.

### Invariants

- A unit has at most one active household.
- A household occupies exactly one unit.
- A sim belongs to at most one household.
- A unit with `IsShelter` can host multiple households up to a configured shelter capacity; V1 models each shelter bed as a unit to keep the one-to-one rule.
- An owner household cannot rent its own unit.
- A child or student must belong to an adult household; independent child households are invalid.
- Capacity overflow is allowed but marks the unit overcrowded.

## Tasks

- [ ] Add enums and components under `Genelife/Components/Housing/`.
- [ ] Add `HousingTierPolicy` with base rent, capacity and quality defaults.
- [ ] Add `Residence` and the synchronous cache-update helper.
- [ ] Add debug invariants for one-household-per-unit and one-unit-per-household.
- [ ] Add component hygiene tests (no managed references except `string`, blittable layout).
- [ ] Update `SimRepository` and population creation to require a household for adults.
- [ ] Document which fields are authoritative and which are derived.

## Files

- new `Genelife/Components/Housing/Property.cs`, `HousingUnit.cs`, `Household.cs`, `HouseholdMember.cs`, `Residence.cs`
- new `Genelife/Enums/HousingTier.cs`, `Tenure.cs`, `HouseholdRole.cs`
- new `Genelife/HousingTierPolicy.cs`
- `Genelife.UnitTests/Components/ComponentHygieneTests.cs`

## Acceptance

- Components compile with no reference-type collections.
- Every adult sim can be resolved to a unit in O(1) via `Residence`.
- Debug invariants catch duplicate occupancy and duplicate membership.
- Default tiers produce a sensible rent ladder.
- Existing sim creation paths still compile.

## Testing

- Component default/value tests.
- Invariant violation tests.
- Tier policy table test.

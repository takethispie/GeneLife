# F04 — Birth and newborn creation

**Phase:** 4 — Birth
**Depends:** F01–F03, T04, R01, `HousingRegistry`
**Estimate:** 2 sessions
**Status:** Done

## Goal

Create a complete, consistent newborn sim and close the pregnancy without special-casing children in later systems.

## Context

The population generator already knows how to create a sim with a name, age, lifecycle, skills, health, wallet and household membership. That logic must be extracted into a reusable factory so births and seeds share one path.

## Design

### Newborn factory

```csharp
public Entity CreateNewborn(
    Entity parentA,
    Entity parentB,
    Entity guardianHousehold,
    Entity unit,
    Traits traits,
    int tick);
```

The factory adds:

- `NetworkId` allocated through the existing registry;
- `GenName` from the child name generator;
- `Alive(0)`;
- `Lifecycle` age 0, `Education` stage, current tick;
- `Traits` inherited through T04;
- `SkillProfile` from the aptitude bonus;
- `Health` at a normal newborn baseline;
- `Needs` defaults;
- `Wallet(Money.Zero)`;
- `HouseholdMember(guardianHousehold, HouseholdRole.Child)`;
- `Residence` when a unit is available;
- `Tenure.Dependent` on the household;
- parent edges to both parents;
- guardian edges when the guardian is not a parent;
- a birth record for the admin/history.

### Birth system

`BirthSystem` at order 380, cadence 100:

1. Collect pregnancies with `Status == ReadyToBirth` or `tick >= DueTick`.
2. For each, in stable network-id order:
   - resolve the carrying and other parent;
   - determine the guardian household and unit;
   - inherit traits;
   - create the newborn in the apply phase;
   - increment household member and unit occupant counts;
   - create parent edges;
   - mark the pregnancy `Completed`;
   - emit `ChildBorn` and `BirthRecorded`.
3. If a parent is dead or missing, use the surviving parent and the current guardian; if none, assign state care in feature 04.

### Capacity

- A birth is never rejected because a unit is full.
- The newborn is placed in the guardian household even if `OccupantCount > Capacity`.
- The housing system sees the overcrowding and reacts through the existing demand path.

### Money

- The newborn starts with zero money.
- A delivery cost may be charged to the household and paid to the provider; if added, it is ledgered and optional.
- No birth path mints money.

### Names

`ChildNameGenerator` uses `SimulationRandom.For("names.child")` keyed by the child network id. Names are deterministic and unique enough for the admin; uniqueness is not a hard constraint.

## Tasks

- [ ] Extract the shared sim factory from `PopulationGenerator`.
- [ ] Add `NewbornFactory` with the full component set.
- [ ] Add `ChildNameGenerator`.
- [ ] Add `BirthSystem` at order 380.
- [ ] Add parent/guardian edges and household membership.
- [ ] Update household/unit counts.
- [ ] Emit birth events and persist the birth record.
- [ ] Add complete-newborn and capacity tests.

## Files

- new `Genelife/Family/NewbornFactory.cs`
- new `Genelife/Family/ChildNameGenerator.cs`
- new `Genelife/Systems/BirthSystem.cs`
- `Genelife.API/Services/PopulationGenerator.cs`
- `Genelife.UnitTests/Family/BirthTests.cs`

## Acceptance

- A newborn has every required component and exactly one `Traits` set.
- Parent edges exist in both directions.
- The newborn is in the guardian household with `HouseholdRole.Child` and `Tenure.Dependent`.
- The pregnancy becomes `Completed` and the birth is recorded.
- A full unit causes overcrowding, not a blocked birth.
- No money is minted.
- The same seed produces the same name and traits.

## Testing

- Birth into a household with capacity.
- Birth into a full unit and assert overcrowding.
- Assert every component and edge.
- Assert the pregnancy is closed and the birth is recorded.
- Replay the same pregnancy id and compare name/traits.
- Assert total money is unchanged.
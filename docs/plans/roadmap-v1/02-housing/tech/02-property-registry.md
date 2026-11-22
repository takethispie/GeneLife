# H02 — Property registry

**Phase:** 1 — Foundation
**Depends:** H01
**Estimate:** 2 sessions
**Status:** Implemented (V1)

## Goal

Provide an O(1) derived index of housing state so rent, assignment, admin reads and metrics never scan the whole world.

## Context

`EmploymentIndex` is the precedent: a plain reference-type index written by systems and read by repositories (`Genelife/Components/Employment/EmploymentIndex.cs`). Housing has many more query directions than employment.

## Design

```csharp
public sealed class HousingRegistry
{
    public Dictionary<Entity, List<Entity>> UnitsByProperty { get; }
    public Dictionary<Entity, Entity> PropertyByUnit { get; }
    public Dictionary<Entity, Entity> HouseholdByUnit { get; }
    public Dictionary<Entity, Entity> UnitByHousehold { get; }
    public Dictionary<Entity, Entity> HouseholdBySim { get; }
    public Dictionary<Entity, List<Entity>> UnitsByOwner { get; }
    public Dictionary<NetworkId, Entity> UnitByNetworkId { get; }

    public void AddProperty(Entity property, Entity owner);
    public void AddUnit(Entity unit, Entity property);
    public void MoveIn(Entity unit, Entity household);
    public void MoveOut(Entity unit);
    public void AddMember(Entity sim, Entity household);
    public void RemoveMember(Entity sim);
    public bool TryGetUnitForSim(Entity sim, out Entity unit);
    public int VacancyCount(HousingTier? tier = null);
    public Money RentRoll(Entity owner);
    public IEnumerable<Entity> VacantUnits(HousingTier? tier = null);
}
```

Rules:

- The registry never owns state; it mirrors components.
- All writes happen in apply phases, never inside `world.Query`.
- Restore rebuilds the registry from components and verifies counts.
- `NetworkId` lookup is populated for units and properties so admin routes are stable.

Add a `HousingRegistrySnapshot` for read models to avoid locking while the tick runs; the engine already snapshots sims and companies.

## Tasks

- [ ] Add `HousingRegistry` with the dictionaries above.
- [ ] Add allocation-light query helpers (no LINQ in hot paths).
- [ ] Add `Rebuild(World)` used by restore and tests.
- [ ] Add consistency checks comparing registry to component state.
- [ ] Wire into `SimulationEngine` and `SimulationManager` alongside `EmploymentIndex`.
- [ ] Add unit/property `NetworkId` allocation on creation.
- [ ] Expose rent roll and vacancy aggregates to `GetWorldData`.

## Files

- new `Genelife/Components/Housing/HousingRegistry.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.API/Services/SimulationManager.cs`
- `Genelife.UnitTests/Components/HousingRegistryTests.cs`

## Acceptance

- All housing lookups used by systems are O(1) or O(units for one property).
- Registry matches components after create/move/evict.
- Restore rebuild produces identical counts and mappings.
- No query-scoped structural mutation.
- Admin read model gets vacancy and rent roll without a full scan.

## Testing

- Add/remove/move sequences.
- Vacancy by tier.
- Rent roll aggregation.
- Rebuild equality.
- Consistency checker negative test.

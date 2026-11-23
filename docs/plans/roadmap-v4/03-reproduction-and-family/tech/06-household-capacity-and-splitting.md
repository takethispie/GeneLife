# F06 — Household capacity and splitting

**Phase:** 6 — Household
**Depends:** F04, R07, `HousingRegistry`, `HousingAllocator`
**Estimate:** 2 sessions
**Status:** Partial — adult-child release and registry repair done; guardian-change moves and full merge retry pending

## Goal

Keep household membership, unit capacity and residence consistent as families grow, merge, split and age, without blocking births or minting housing.

## Context

`HousingUnit.Capacity`, `OccupantCount`, `IsOvercrowded`, `Household.MemberCount` and `HousingRegistry` already exist. `HousingAllocator` assigns vacant units; `HousingMoveSystem` handles ordinary moves.

## Design

### FamilyHouseholdSystem

Order 385, cadence 100 (monthly):

1. Retry pending partner merges (R07).
2. Move adults and children whose guardian changed.
3. Split households when a death/separation left an invalid composition.
4. Move adult children out under the configured policy.
5. Flag overcrowded units and request housing through the existing demand path.
6. Rebuild/repair the registry counts if an invariant fails.

### Capacity

- A merge requires capacity for the moving sim plus dependants.
- A birth never requires capacity; the newborn joins the guardian household and may overcrowd the unit.
- Overcrowding is a warning/metric, not a block.
- A split creates a new household; it needs a unit from the allocator.
- If no unit is available, the new household is homeless and visible in housing metrics.

### Adult children

- At 18, a sim may remain as an adult member or form a new household.
- Policy options:
  - `AdultChildMoveOutShare` (default 0.50);
  - `MinimumHomeCapacityForAdultChild` (default 1 spare room).
- If the family home has capacity, the adult child may stay.
- If not, the sim requests a unit; if none, they remain overcrowded or become homeless, both visible.

### Guardianship changes

- If a guardian dies, feature 04 selects a new guardian before the monthly reconciliation.
- The child moves with the guardian, not with the deceased parent's household.
- Parent edges do not change.

### Money

- Rent stays with the household.
- A split may trigger the existing deposit/move path.
- No capacity change creates or destroys money.

## Tasks

- [ ] Add `FamilyHouseholdSystem` at order 385.
- [ ] Add pending-merge retry.
- [ ] Add guardian-change moves.
- [ ] Add adult-child move-out policy.
- [ ] Add overcrowding demand hooks.
- [ ] Add registry repair/verify.
- [ ] Add capacity, split and money tests.

## Files

- new `Genelife/Systems/FamilyHouseholdSystem.cs`
- new `Genelife/Family/FamilyHousingPolicy.cs`
- `Genelife/Components/Housing/HousingRegistry.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.UnitTests/Family/FamilyHousingTests.cs`

## Acceptance

- A merge occurs as soon as capacity exists.
- A birth into a full unit creates overcrowding and does not fail.
- An adult child moves out only when the policy and capacity allow it.
- A split produces a valid household or a visible homeless household.
- Registry counts and vacancy agree after every reconciliation.
- No money is minted by a capacity change.

## Testing

- Merge after a unit becomes vacant.
- Birth into a full unit and assert `IsOvercrowded`.
- Adult child with and without spare capacity.
- Guardian death and child move.
- Run `HousingRegistry.Verify` after each scenario.
- Assert the money supply is unchanged.
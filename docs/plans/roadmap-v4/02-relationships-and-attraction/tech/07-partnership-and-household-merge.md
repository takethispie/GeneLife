# R07 — Partnership and household merge

**Phase:** 7 — Household
**Depends:** R01, R02, R06
**Estimate:** 2 sessions
**Status:** Planned (V4)

## Goal

When partners form, merge their households when capacity allows and keep the partnership valid when it does not.

## Context

`HousingRegistry` owns membership, occupancy and moves. `HousingUnit.Capacity` and `OccupantCount` already support crowding checks. `Household` is the rent and arrears unit.

## Design

### Merge service

```csharp
public bool TryMergePartners(Entity partnerA, Entity partnerB, long tick)
```

1. Resolve both households and units through the housing registry.
2. If already in the same household, return true.
3. Score the target home:
   - owned home first;
   - enough capacity for the moving sim and any dependants;
   - larger unit;
   - lower `NetworkId`.
4. If no target has capacity, return false and leave the partnership intact.
5. Move the lower-priority sim into the target household:
   - `RemoveMember` from the source household and add to the target;
   - update `HouseholdMember.Role` to `Partner`;
   - update `Household.MemberCount`;
   - move the sim's `Residence` cache;
   - update `HousingUnit.OccupantCount` and vacancy.
6. Preserve the target head. If the moving sim was a head, reassign the source head deterministically.
7. If the source household becomes empty, release its unit through the existing move-out path.
8. Charge no family-merge fee; existing ordinary move fees remain unchanged.

### Retry

Partners in different households are retried monthly by `FamilyHouseholdSystem` (feature 03, order 385):

- if a larger unit becomes available, merge;
- if a partner dies, split;
- if a unit is condemned, move.

### Split service

```csharp
public bool SplitHousehold(Entity household, Entity leavingSim, long tick)
```

- Create a new household with the leaving sim as head.
- Preserve parent/guardian edges.
- Dependants stay with the guardian unless an admin or death rule says otherwise.
- Allocate a unit through the existing allocator; if none, the new household is homeless and visible to housing metrics.
- Update rent, occupancy and registry in one apply phase.

### Money

- Rent remains the target household's rent after merge.
- No new money is created.
- Moving fees are waived for a family merge; the ledger records no transfer.
- If a split requires a new deposit, the existing move path decides whether the household can afford it.

## Tasks

- [ ] Add `HouseholdMergeService` with merge and split methods.
- [ ] Add target-home selection and tie-breaks.
- [ ] Update registry membership, counts, residence and vacancy.
- [ ] Reassign heads deterministically.
- [ ] Add the monthly retry hook in `FamilyHouseholdSystem`.
- [ ] Add merge/split and capacity tests.
- [ ] Add a conservation assertion around the housing move.

## Files

- new `Genelife/Family/HouseholdMergeService.cs`
- new `Genelife/Systems/FamilyHouseholdSystem.cs`
- `Genelife/Components/Housing/HousingRegistry.cs`
- `Genelife.UnitTests/Family/HouseholdMergeTests.cs`

## Acceptance

- Two partners in homes with spare capacity end up in one household.
- A partner moves when the target unit has capacity for the whole dependant set.
- No merge occurs when neither home has capacity.
- The target head is preserved unless the head moved.
- Occupant counts, vacancy, member counts and residence agree after every merge.
- A split creates a valid household and does not orphan a dependant.
- No money is minted.

## Testing

- Merge into an owned home and into a rented home.
- Merge with a child dependant and assert capacity.
- Reject a merge at exact capacity plus one.
- Split a household and assert head, unit and dependants.
- Run the registry `Verify` after each operation.
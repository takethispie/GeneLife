# Partnerships and households

A partnership is a relationship; a shared household is a housing outcome. V4 keeps them separate on purpose. Two sims can be partners without sharing a unit, and a household can contain people who are not partners.

## Why separate the two

- Housing capacity is often the constraint. A couple may have to wait for a larger unit.
- A partnership can survive a move, a job change or a temporary separation.
- Inheritance and guardianship follow relationships, not co-residence.
- A household merge is a money and rent event; a partnership is a social fact.

## Merge rules

When a partnership forms, the system attempts a merge:

1. Prefer the home with enough capacity for both sims and any dependants.
2. If both qualify, prefer the owned home, then the larger unit, then the lower `NetworkId`.
3. Move the lower-priority sim into the target household.
4. Preserve the existing head unless the head is the moving sim.
5. Update `HouseholdMember`, `Household.MemberCount`, `HousingUnit.OccupantCount`, `Residence` and the housing index in one apply phase.
6. Keep the rent and tenure of the target household.
7. If neither home has capacity, keep two households and retry later.

## Split rules

A household splits when:

- a partner dies and the surviving adults cannot remain together;
- an admin ends a partnership and requests separation;
- a dependant reaches adulthood and leaves;
- a unit is condemned or the household is evicted.

The split creates a new household, assigns a head deterministically, preserves dependants with their guardian and updates rent and occupancy.

## Children

- A child belongs to the household of their guardian, not necessarily to both parents.
- Parent edges are independent of household membership.
- A child can move with a guardian without changing parent edges.
- School enrollment follows the child, not the household.

## What the operator should see

- partnerships that are split across households and why;
- households that are over capacity;
- merge attempts that failed for lack of capacity;
- rent and occupancy changes caused by a merge or split;
- the relationship graph for a selected household.

## What V4a does not do

- No forced relocation without capacity.
- No rent renegotiation on merge beyond the existing rent policy.
- No divorce litigation or custody dispute.
- No player-facing relationship status or partner search.
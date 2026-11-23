# Family formation and housing

A partnership is not a household, and a child is not a room. V4 has to connect the social graph to the housing system without pretending that capacity is unlimited or that every couple can immediately live together.

## What happens when a couple forms

1. The partnership exists immediately, regardless of housing.
2. The system tries to merge households if a unit can hold both partners and their dependants.
3. If no unit has capacity, the couple remains split and the merge is retried monthly.
4. A child is born into the guardian's household, even if the unit is overcrowded.
5. Overcrowding becomes visible housing demand, not a blocked birth.

## Why not block birth on capacity

Blocking a birth because a unit is full turns a housing shortage into a fertility rule. It also creates a weird incentive: the simulation would decide whether a child exists based on square metres. V4 allows temporary overcrowding, records it, and lets the housing market respond.

## Household roles

- `Head` — the rent and decision anchor.
- `Partner` — the other adult in the household.
- `Child` — a dependant.
- `Relative` — another adult, such as a grandparent.
- `Other` — a fallback.

A child can have two parents in different households. The guardian household is the one that provides the residence; parent edges are independent.

## Moving out

- A child stays in the guardian household until adulthood.
- At adulthood, the sim may remain as an adult member or form a new household, depending on capacity and policy.
- A new household needs a unit; if none is available, the sim is homeless and visible in housing metrics.
- Moving out is not a relationship break. Parent and sibling edges remain.

## Money

- Rent is the household's responsibility, not the child's.
- A merge does not renegotiate rent in V4a.
- A split may require a new deposit through the existing move path.
- Child costs are transfers inside the existing money loop.

## What the operator should see

- partners living apart and why;
- households at or over capacity;
- children whose parents live in different households;
- adult children who have not moved out;
- moves caused by a merge, split or new unit.
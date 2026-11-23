# Why relationships

V1 and V2 treat a household as a cost-sharing unit and a child as a household member. That is enough for rent, school and inheritance, but it is not a family. A child has no parents, a partner is only a role label, and inheritance is “whoever happens to share the household.”

Relationships add the missing social structure: who is a parent, who is a partner, who is a guardian, and who is related to whom. Everything else in V4 depends on it.

## What relationships add

- **A real family graph.** Parent and child edges survive household changes, moves and deaths.
- **Meaningful inheritance.** Heirs can be partners and children rather than co-residents.
- **Household formation.** Partners can merge homes, share rent and raise children.
- **Guardianship.** A minor can have a responsible adult even when the household changes.
- **Explainable continuity.** A family line is visible across generations.
- **A basis for later social features.** Friendship, rivalry or reputation can be added as new edge kinds without rewriting the graph.

## What this is not

- It is not a dating app. There is no player swipe, match list or public compatibility ranking.
- It is not a forced romance system. Pairing is an emergent consequence of eligibility and score.
- It is not a replacement for households. A relationship can exist across two households when housing capacity does not allow a merge.
- It is not a complete genealogy product. The admin can inspect the graph; a later player-facing client may show family and life events, but V4a keeps the graph admin-only and has no tree editor.

## Why edges instead of fields

A sim could carry `Parent`, `Partner` and `Child` fields, but that breaks quickly:

- a sim can have two parents, several children and a guardian;
- a partner is a mutual fact;
- a deceased parent is still a parent;
- an ex-partner is a historical fact, not the absence of a field.

Directed edge entities keep the graph extensible and let one index answer both directions.

## What the operator should be able to see

- who is partnered with whom;
- who are a sim's parents, children and siblings;
- which relationships came from the seed, attraction, admin or migration;
- why two sims did or did not pair;
- when a partnership caused a household merge.
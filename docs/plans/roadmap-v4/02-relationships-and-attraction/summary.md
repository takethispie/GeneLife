# Relationships and Attraction — Summary

**Folder:** `02-relationships-and-attraction/`
**Status:** Planned (V4)

**Depends on:** feature 01 traits and genetics, `SpatialIndex`, `HousingRegistry`, `EmploymentIndex`, lifecycle stages
**Feeds:** reproduction and family, continuity and inheritance, admin family page

## Objective

Give sims a real family and social graph and a deterministic, explainable way to form partnerships. Relationships are stored facts, not inferred from shared households; attraction is a bounded score with named components, not a black box.

This feature produces the edges that reproduction, inheritance and the family read model consume: parent, child, partner and, later, guardian/ward.

## Baseline before V4

- No relationship component, index or edge entity exists.
- `HouseholdMember.Role` already has `Partner` and `Child`, but roles are not backed by parent or partner edges.
- `PopulationGenerator` creates one household per adult and assigns children to random adult households.
- `HousingUnit.Capacity` and `OccupantCount` exist; `HousingRegistry` owns membership and moves.
- `SpatialIndex`, `EmploymentIndex` and `LifecycleIndex` provide candidate sources.
- `AttractivenessService` is an offline prototype; no runtime scorer exists.

## Deliverables

1. Directed relationship edge model and reverse-indexed relationship index.
2. Seeded family structure for generated populations: partnerships and parent edges.
3. Eligibility policy covering age, consanguinity, exclusivity and pairing rules.
4. Deterministic integer attraction score with explainable components.
5. Candidate generation from households, workplaces, schools and spatial neighbours.
6. Pairing system with mutual acceptance, cooldowns and deterministic tie-breaks.
7. Partnership-to-household merge and split, respecting capacity and tenure.
8. Relationship events and Marten projections.
9. Admin read model, API and family page foundations.
10. Relationship metrics, invariants and health checks.
11. Acceptance tests for determinism, index consistency and household safety.

## Architecture

```
Traits + Age + Household + Job/School + TilePosition
                         │
                         ▼
              Candidate generation (capped)
                         │
                         ▼
          Attraction score (integer components)
                         │
                         ▼
        Pairing decision + deterministic acceptance
                         │
             ┌───────────┴───────────┐
             ▼                       ▼
     Relationship edges        Household merge/split
             │                       │
             ▼                       ▼
     RelationshipIndex        HousingRegistry / rent
             │
             ▼
   Events, projections, admin read model
```

`RelationshipIndex` is derived, like `EmploymentIndex`: it mirrors edge components, is rebuilt on restore and is updated only in apply phases.

## Tech steps

| # | Step | Phase |
|---|---|---|
| 01 | [Relationship domain model](tech/01-relationship-domain-model.md) | Foundation |
| 02 | [Relationship index](tech/02-relationship-index.md) | Foundation |
| 03 | [Eligibility and seeded family policy](tech/03-eligibility-and-seeded-family-policy.md) | Policy |
| 04 | [Attraction score](tech/04-attraction-score.md) | Scoring |
| 05 | [Candidate generation and budgets](tech/05-candidate-generation-and-budgets.md) | Scoring |
| 06 | [Attraction and pairing system](tech/06-attraction-and-pairing-system.md) | Pairing |
| 07 | [Partnership and household merge](tech/07-partnership-and-household-merge.md) | Household |
| 08 | [Relationship events and projections](tech/08-relationship-events-and-projections.md) | Events |
| 09 | [Relationship admin API and UI](tech/09-relationship-admin-api-and-ui.md) | Admin |
| 10 | [Relationship metrics and invariants](tech/10-relationship-metrics-and-invariants.md) | Observability |
| 11 | [Relationships acceptance](tech/11-relationships-acceptance.md) | Verification |

## Business reading

- [Why relationships](business/01-why-relationships.md)
- [Attraction without a black box](business/02-attraction-without-a-black-box.md)
- [Partnerships and households](business/03-partnerships-and-households.md)

## Definition of done

- Every seeded child has parent edges, and a configured share of seeded adults has partner edges.
- Every edge has one component and one mirror edge; the index matches the components after restore.
- Eligibility rejects minors, close relatives, existing partners and invalid ages before scoring.
- Attraction returns a bounded integer score with named components and deterministic results.
- Candidate generation never becomes an all-pairs scan and stays within the configured cap.
- Pairing creates or rejects consistently, emits events and does not duplicate edges.
- A partnership merges households when capacity allows and records the partnership when it does not.
- The family read model and admin page expose relationships, partnerships and score explanations.
- Metrics and invariants catch mirror, duplicate, cycle and capacity defects.
- Acceptance scenarios, restore and the 10-year balance scenario pass.
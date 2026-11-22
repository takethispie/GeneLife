# Housing — Summary

**Folder:** `02-housing/`
**Status:** Implemented (V1)

**Depends on:** economy payroll and accounts (`01-economy/tech/05`, `07`), population generation (life stages)
**Feeds:** needs and health modifiers, mortality, household wealth, estate settlement

## Objective

Give every NPC a home: a unit in a building, a household that shares costs, a monthly rent or ownership burden, and a real risk of losing it. Housing is the main recurring expense that turns wages into a life constraint.

## Baseline before V1

- Sims have a `Location` and the world has a `SpatialIndex`, but there are no buildings.
- Companies have offices; there is no residential property, landlord or rent.
- `Needs` decay uniformly; a mansion and a shelter have no effect.
- Wealth lives in `Wallet` only; there is no household pooling.

## Deliverables

1. `Property` and `HousingUnit` entities with quality, capacity, rent and condition.
2. `HousingRegistry` derived index for occupancy, vacancy, rent roll and arrears.
3. `Household` entity with a head, members and pooled finances.
4. Initial assignment of population to households and units.
5. Monthly rent with a payment waterfall and arrears.
6. Utilities and maintenance as separate line items.
7. Housing quality effects on needs decay, health and recovery.
8. Eviction, shelter capacity and homelessness.
9. Voluntary moves and tenure changes.
10. Housing supply, construction and a rent index.
11. Persistence and restore.
12. Admin housing page with property and tenancy actions.
13. Housing metrics and health checks.
14. Scenario tests and acceptance.

## Architecture

```
Property (building)
   └── HousingUnit (one tenancy) ── Residence ── Sim / Household
                                             │
             RentSystem ──► Household wallet ─┘
                  │
                  ├── landlord account (company or private owner)
                  ├── arrears / eviction
                  └── HousingRegistry (read model)
```

## Tech steps

| # | Step | Phase |
|---|---|---|
| 01 | [Housing domain model](tech/01-housing-domain-model.md) | Foundation |
| 02 | [Property registry](tech/02-property-registry.md) | Foundation |
| 03 | [Households](tech/03-households.md) | Households |
| 04 | [Initial assignment](tech/04-initial-assignment.md) | Households |
| 05 | [Rent and tenure cycle](tech/05-rent-and-tenure-cycle.md) | Tenancy |
| 06 | [Utilities and upkeep](tech/06-utilities-and-upkeep.md) | Tenancy |
| 07 | [Housing quality effects](tech/07-housing-quality-effects.md) | Tenancy |
| 08 | [Arrears, eviction and shelter](tech/08-arrears-eviction-and-shelter.md) | Risk |
| 09 | [Moving and ownership](tech/09-moving-and-ownership.md) | Mobility |
| 10 | [Supply and construction](tech/10-supply-and-construction.md) | Market |
| 11 | [Persistence](tech/11-persistence.md) | Durability |
| 12 | [Admin API and UI](tech/12-admin-api-and-ui.md) | Admin |
| 13 | [Metrics and health](tech/13-metrics-and-health.md) | Observability |
| 14 | [Scenario tests](tech/14-scenario-tests.md) | Verification |
| 15 | [Acceptance](tech/15-acceptance.md) | Verification |

## Business reading

- [The home as economic anchor](business/01-home-as-economic-anchor.md)
- [Tenure and mobility](business/02-tenure-and-mobility.md)
- [Neighbourhood quality](business/03-neighbourhood-quality.md)
- [Housing market dynamics](business/04-market-dynamics.md)

## Definition of done

- Every adult NPC belongs to exactly one household and one housing unit or shelter.
- Rent is charged monthly, paid from household cash, and produces arrears when unpaid.
- Persistent non-payment leads to eviction and a visible homeless state.
- Housing quality measurably changes needs decay, health and recovery.
- Vacancy, rent burden and homelessness stay inside the target bands for a 10-year run.
- Admin can create property, set rent, assign and evict tenants, and inspect the rent roll.
- Save/restore preserves households, tenancies and arrears.

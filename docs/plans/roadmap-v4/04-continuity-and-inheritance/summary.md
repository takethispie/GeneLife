# Continuity and Inheritance — Summary

**Folder:** `04-continuity-and-inheritance/`
**Status:** Implemented core (V4 feature 04; 30-year/2,000-sim runs, projections, alerts and UI remain staged)

**Depends on:** features 01–03, `PopulationInflowSystem`, `PopulationMetricsSystem`, `DeathProcessingSystem`, `EstateSettlementSystem`, `HousingRegistry`
**Feeds:** feature 05, long-run balance, admin lifecycle/family pages

## Objective

Make births a real population-continuity path and make death settle through the family graph. A sim should be able to replace the population from within, pass assets to a partner or child, and leave a minor with a guardian instead of an orphaned household.

This feature closes the loop opened by reproduction: children grow up, inherit, form their own households and eventually replace the previous generation.

## Baseline before V4

- `PopulationInflowSystem` is the only long-run replacement path.
- `PopulationSnapshot` has deaths and arrivals but no births.
- `DeathProcessingSystem` removes a member, reassigns the household head and computes heirs from current household members.
- `EstateSettlementSystem` pays debts, funeral, tax, heirs and escheat; owned housing transfers to a surviving member.
- There is no guardianship, no minor trust and no relationship-based heir order.
- The 2026-11 balance scenario covers money, housing and employment, not family continuity.

## Deliverables

1. Birth-aware immigration policy and population target semantics.
2. Partner-death transition: partnership end, head reassignment, guardian selection.
3. Relationship-based inheritance order with partner/child shares.
4. Minor trust accounts released at adulthood.
5. Orphan and guardianship handling, including state care.
6. Owned-home and household transition rules on death.
7. Birth, death, immigration and family continuity metrics.
8. Long-run population balance scenario (10 and 30 years).
9. Invariants and alerts for orphans, missing guardians and trust conservation.
10. Acceptance for continuity, inheritance and restore.

## Architecture

```
Births ──► PopulationInflowSystem ◄── Immigration
   │                │
   └──────► PopulationSnapshot ◄── Deaths
                         │
                         ▼
              DeathProcessingSystem
                 │            │
                 ▼            ▼
        Relationship end   Guardian selection
                 │            │
                 └────► EstateSettlementSystem
                              │
                    ┌─────────┼─────────┐
                    ▼         ▼         ▼
                 Partner   Children   Escheat
                              │
                              ▼
                        Minor trust / guardian
```

## Tech steps

| # | Step | Phase |
|---|---|---|
| 01 | [Births vs immigration policy](tech/01-births-vs-immigration-policy.md) | Continuity |
| 02 | [Partner death and household transition](tech/02-partner-death-and-household-transition.md) | Family |
| 03 | [Inheritance rules](tech/03-inheritance-rules.md) | Inheritance |
| 04 | [Orphans and guardianship](tech/04-orphans-and-guardianship.md) | Family |
| 05 | [Long-run population balance](tech/05-long-run-population-balance.md) | Balance |
| 06 | [Continuity metrics and alerts](tech/06-continuity-metrics-and-alerts.md) | Observability |
| 07 | [Continuity acceptance](tech/07-continuity-acceptance.md) | Verification |

## Business reading

- [Population continuity](business/01-population-continuity.md)
- [Inheritance and family assets](business/02-inheritance-and-family-assets.md)

## Definition of done

- Births are counted, projected and included in the population target logic.
- Immigration remains a floor-filling path, not the only source of adults.
- A partner's death ends the active partnership, reassigns the head and guardian, and preserves parent edges.
- Inheritance follows the relationship graph, pays debts/funeral/tax first, and conserves money.
- A minor's share is held in trust and released at adulthood.
- An orphan gets a guardian, a relative or state care; none is silently dropped.
- Continuity metrics and invariants are visible in the admin.
- The 10-year and 30-year scenarios pass without population collapse or money leakage.
- Acceptance, restore and schema migration pass.
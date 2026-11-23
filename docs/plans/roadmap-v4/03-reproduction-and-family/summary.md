# Reproduction and Family — Summary

**Folder:** `03-reproduction-and-family/`
**Status:** Implemented core (V4 feature 03; projections, admin mutations/UI, yearly metrics and cohabitation recalibration remain staged)

**Depends on:** features 01 and 02, `Household`, `HousingUnit.Capacity`, `HousingRegistry`, `School`, `Health`, `HouseholdFinanceService`
**Feeds:** continuity and inheritance, family read model, long-run population balance

## Objective

Let partners have children, raise them in households, send them to school and release them into adult life. Birth must create a complete, consistent sim with inherited traits, parent edges, a guardian household and a real cost path — not a special-case entity that later systems cannot understand.

## Baseline before V4

- No pregnancy, birth or newborn creation path exists.
- `PopulationGenerator` creates the only children; they are assigned to random adult households without parent edges.
- `HouseholdRole.Child` and `Tenure.Dependent` exist.
- `HousingUnit.Capacity` and `OccupantCount` exist; `IsOvercrowded` is already computed.
- `EnrollmentSystem` guards on `Alive.Age >= SchoolStartAge`; `EducationSystem` trains `SkillProfile`.
- `HouseholdFinanceService` owns rent, utilities, arrears and the monthly income calculation.
- `School` charges a monthly cost; health care is purchased through the health system.

## Deliverables

1. Pregnancy entity, status and active-pregnancy index.
2. Deterministic monthly conception system for eligible partners.
3. Pregnancy progression, risk, care cost and loss handling.
4. Birth system and a reusable newborn factory.
5. Trait inheritance and aptitude-skewed starting skills on birth.
6. Parent and guardian edges for the newborn.
7. Childhood dependency, school entry and adult transition.
8. Household capacity, overcrowding and adult-child move-out.
9. Child costs through existing household, school and health flows; no money creation.
10. Pregnancy/birth events, projections and admin read model.
11. Family metrics, invariants and acceptance scenarios.

## Architecture

```
Partners (feature 02)
        │
        ▼
ConceptionSystem (monthly, deterministic)
        │
        ▼
Pregnancy entity ──► PregnancySystem ──► BirthSystem
        │                  │                 │
        │                  ▼                 ▼
        │            care cost        Newborn factory
        │                              ├─ Traits inheritance
        │                              ├─ parent edges
        │                              ├─ guardian/household
        │                              └─ aptitude skills
        ▼
Pregnancy/birth projections ──► Family read model
```

`Pregnancy` is an entity because it has its own lifecycle, network id, risk state and admin history. A component on the carrying sim would make history and loss tracking awkward.

## Tech steps

| # | Step | Phase |
|---|---|---|
| 01 | [Pregnancy domain model](tech/01-pregnancy-domain-model.md) | Foundation |
| 02 | [Conception system](tech/02-conception-system.md) | Conception |
| 03 | [Pregnancy progression and health](tech/03-pregnancy-progression-and-health.md) | Pregnancy |
| 04 | [Birth and newborn creation](tech/04-birth-and-newborn-creation.md) | Birth |
| 05 | [Childhood and life stages](tech/05-childhood-and-life-stages.md) | Family |
| 06 | [Household capacity and splitting](tech/06-household-capacity-and-splitting.md) | Household |
| 07 | [Child costs and household finance](tech/07-child-costs-and-household-finance.md) | Economy |
| 08 | [Reproduction events and projections](tech/08-reproduction-events-and-projections.md) | Events |
| 09 | [Reproduction admin API and UI](tech/09-reproduction-admin-api-and-ui.md) | Admin |
| 10 | [Family metrics and invariants](tech/10-family-metrics-and-invariants.md) | Observability |
| 11 | [Reproduction acceptance](tech/11-reproduction-acceptance.md) | Verification |

## Business reading

- [Why births](business/01-why-births.md)
- [Family formation and housing](business/02-family-formation-and-housing.md)
- [Childhood and household costs](business/03-childhood-and-household-costs.md)

## Definition of done

- Eligible partners can conceive and a pregnancy progresses to birth or loss deterministically.
- A newborn has traits, parent edges, a guardian household, a zero wallet, an education-stage lifecycle and a dependent tenure.
- Childhood dependency, school entry and the transition to work use existing systems with explicit age gates.
- Household capacity and overcrowding are visible; a birth is never blocked by capacity.
- Child costs are ledgered transfers; no path mints money.
- Pregnancy and birth events are projected and readable in the admin.
- Family metrics and invariants catch missing guardians, duplicate pregnancies and child work.
- Acceptance, restore and the 10-year balance scenario pass.
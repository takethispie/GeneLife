# Life Stages — Summary

**Folder:** `03-life-stages/`
**Status:** Implemented (V1)

**Depends on:** economy labour market (`01-economy/tech/03`, `04`, `07`, `09`, `13`), housing (`02-housing/tech/03`, `07`)
**Feeds:** population metrics, immigration, estate settlement, health-driven mortality

## Objective

Give every NPC a full lifecycle: education, working life, retirement and elderly decline, ending in death and estate settlement. Age is the clock that connects jobs, housing, health and money over decades.

## Model

| Stage | Age | Primary role | Income | Housing tenure |
|---|---|---|---|---|
| Education | 0–17 (school from 6) | dependent, student | household-funded | dependent |
| Work | 18–64 | worker, job seeker | wages, benefits | renter/owner |
| Retirement | 65–79 | retiree | pension + savings | renter/owner |
| Elderly | 80+ | elder | pension + savings + care | renter/owner/care |

Four stages only. Children are the early part of Education; there is no separate Child stage.

## Baseline before V1

- `Alive` stores `Age` but nothing increments it.
- `JobSeekerSystem` creates job seekers for `Age >= 18` (`Genelife/Systems/JobSeekerSystem.cs:42`) with hard-coded skills and desired salary.
- No schools, curriculum, skill levels, work schedule, promotions, retirement, pension, health, mortality or inheritance.
- `ActionType` has no `Working`.
- Population can only be created manually via `SimRepository.Add`.

## Deliverables

1. `LifeStage` component and four-stage state machine.
2. Aging clock derived from `SimulationClock`.
3. Idempotent stage transitions with enter/exit handlers.
4. Schools, enrollment, attendance and curriculum.
5. Skill profile with proficiency levels, replacing the bare `Skill` bitmask for match scoring.
6. Graduation and labour-market entry.
7. Work stage behaviour: `Working` action, schedule, attendance, performance.
8. Promotions, salary progression, separation and unemployment.
9. Retirement transition, pension accrual and payout from the funded pool.
10. Elderly decline, health and care.
11. Mortality from age, health, needs and housing.
12. Estate settlement: debts, heirs, household, escheat.
13. Population inflow to keep a realistic age pyramid.
14. Population generator v2 with household composition.
15. Persistence and migration.
16. Admin lifecycle page and controls.
17. Lifecycle metrics and balance guardrails.
18. Scenario tests and acceptance.

## Architecture

```
AgingSystem (90)
   └── AgeTicks → Alive.Age
          │
StageProgressionSystem (390)
   ├── Education ──► School, Curriculum, Graduation
   ├── Work ───────► JobSeeker, HiringSystem, Payroll, Career
   ├── Retirement ─► PensionAccount, PensionSystem
   └── Elderly ────► Health, ElderDecline, Mortality
                              │
                              ▼
                    EstateSettlementSystem (580)
```

## Tech steps

| # | Step | Phase |
|---|---|---|
| 01 | [Life stage domain model](tech/01-life-stage-domain-model.md) | Foundation |
| 02 | [Aging clock](tech/02-aging-clock.md) | Foundation |
| 03 | [Stage progression](tech/03-stage-progression.md) | Foundation |
| 04 | [Education enrollment](tech/04-education-enrollment.md) | Education |
| 05 | [Curriculum and skill gain](tech/05-curriculum-and-skill-gain.md) | Education |
| 06 | [Graduation and labour entry](tech/06-graduation-and-labour-entry.md) | Education |
| 07 | [Work stage behaviour](tech/07-work-stage-behaviour.md) | Work |
| 08 | [Attendance and performance](tech/08-attendance-and-performance.md) | Work |
| 09 | [Promotion and pay progression](tech/09-promotion-and-pay-progression.md) | Work |
| 10 | [Separation and unemployment](tech/10-separation-and-unemployment.md) | Work |
| 11 | [Retirement transition](tech/11-retirement-transition.md) | Retirement |
| 12 | [Pension accrual and fund](tech/12-pension-accrual-and-fund.md) | Retirement |
| 13 | [Pension payout](tech/13-pension-payout.md) | Retirement |
| 14 | [Elderly decline and health](tech/14-elderly-decline-and-health.md) | Elderly |
| 15 | [Mortality and death processing](tech/15-mortality-and-death-processing.md) | Elderly |
| 16 | [Estate settlement and inheritance](tech/16-estate-settlement-and-inheritance.md) | Elderly |
| 17 | [Population inflow](tech/17-population-inflow.md) | Continuity |
| 18 | [Population generator v2](tech/18-population-generator-v2.md) | Continuity |
| 19 | [Persistence and restore](tech/19-persistence-and-restore.md) | Durability |
| 20 | [Admin API and UI](tech/20-admin-api-and-ui.md) | Admin |
| 21 | [Metrics and balance](tech/21-metrics-and-balance.md) | Observability |
| 22 | [Testing and acceptance](tech/22-testing-and-acceptance.md) | Verification |

## Business reading

- [Lifecycle and roles](business/01-lifecycle-and-roles.md)
- [Education](business/02-education.md)
- [Working life](business/03-working-life.md)
- [Retirement and pensions](business/04-retirement-and-pensions.md)
- [Elderly and mortality](business/05-elderly-and-mortality.md)
- [Population continuity](business/06-population-continuity.md)

## Definition of done

- Age advances and every sim is in exactly one stage matching their age.
- Students enroll, learn, graduate and enter the labour market with a skill profile.
- Workers have schedules, attendance, performance and career progression.
- Retirees stop working and receive a funded pension.
- Elders decline, die from age/health/needs, and their estate is settled.
- Population remains stable over 10 simulated years through inflow and labour demand.
- All stages and controls are visible and operable in the admin UI.
- Save/restore replays stages without duplication or skipped transitions.

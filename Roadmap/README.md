# Population Simulation Roadmap V1

**Target:** Working-life simulation of thousands of Genes (player/NPC persons) across 4 life stages, with a closed-loop economy integrated into the existing hiring, office, and payroll systems.

---

## Scope

V1 delivers a complete but barebones working system. Every feature lands in a playable state — no stubs, no "later" placeholders. Full-game ambitions are deliberately excluded.

### In Scope

- 4-stage Gene lifecycle: Education → Work → Retirement → Elder
- Age-gated stage transitions with automatic behaviors
- Closed-loop economy (spending → revenue → wages)
- Basic housing tied to economic status
- Population generation with demographic spread
- Mortality and inheritance
- Capital/wealth tracking (banking)
- Skills system integrated with hiring

### Out of Scope (Future Versions)

- Relationships, attraction, reproduction
- Genetics / DNA merging
- Utilities (water/electricity)
- Advanced grocery / inventory
- Multiplayer-specific behaviors (networking already exists, just no player-driven life decisions)

---

## Architecture Alignment

This roadmap builds on the existing ECS architecture:

| Existing System | Used By |
|---|---|
| `Alive` (Age) | Stage transitions, mortality |
| `Needs` (Hunger/Energy/Hygiene/Bladder) | All stages — decay and fulfillment |
| `DecisionSystem` + `ActionSystem` | NPC autonomous behavior per stage |
| `Employee` + `HiringSystem` | Work stage integration |
| `PayrollSystem` | Wage income during Work |
| `Wallet` | Consumer spending, savings |
| `CompanyName` + `Office` | Workplace for employed Genes |
| `SpatialIndex` | Building placement, Gene movement |
| `NetworkId` | Identity across persistence |

### New Systems Map

```
                            ┌─────────────────┐
                            │  StageProgression│
                            │     System       │
                            └────────┬────────┘
                                     │ age-gates transition
          ┌──────────────────────────┼──────────────────────────┐
          ▼                          ▼                          ▼
   ┌──────────────┐          ┌──────────────┐          ┌──────────────┐
   │  Education   │          │     Work     │          │  Retirement  │
   │   System     │          │   System     │          │   System     │
   └──────┬───────┘          └──────┬───────┘          └──────┬───────┘
          │                         │                         │
          │ skills                  │ existing                 │ pension
          ▼                         ▼                         ▼
   ┌──────────────┐          ┌──────────────┐          ┌──────────────┐
   │SkillComponent│          │HiringSystem  │          │PensionSystem │
   │              │          │PayrollSystem │          │              │
   └──────────────┘          │ConsumerSystem│          └──────────────┘
                             │RevenueSystem │
                             └──────────────┘

                                     │
                          ┌──────────┴──────────┐
                          ▼                     ▼
                   ┌────────────┐        ┌────────────┐
                   │ Mortality  │        │ Inheritance│
                   │  System    │        │  System    │
                   └────────────┘        └────────────┘
```

---

## Files

| Folder | Contents |
|---|---|
| [`01-life-stages/`](01-life-stages/) | Education, Work, Retirement, Elder stage designs |
| [`02-economy/`](02-economy/) | Consumer spending, company revenue, banking, housing costs |
| [`03-population/`](03-population/) | World generation, demographic profiles, NPC archetypes |
| [`04-life-events/`](04-life-events/) | Stage transitions, mortality rules, inheritance |

---

## Implementation Order

Dependencies between systems dictate this build order:

```
1. Skill Component + Education System
       │
2. Consumer System + Housing (foundational spending)
       │
3. Work Stage (extends existing hiring/payroll with skills & working hours)
       │
4. Company Revenue System (closes spending loop)
       │
5. Banking/Pension System (savings, retirement income)
       │
6. Stage Transition System (age-gated progression)
       │
7. Mortality + Inheritance (end-of-life cleanup)
       │
8. Population Generator (seeds world with diverse demographic spread)
```

Steps 1-3 can partially overlap. Steps 4-6 depend on prior steps. Steps 7-8 are independent of each other but both need step 6.

---

## Conventions

- New components in `Genelife/Components/` under appropriate subfolders
- New systems in `Genelife/Systems/` following existing cadence patterns
- Each system file includes its unit tests in `Genelife.UnitTests/`
- All monetary values use `float` (consistent with existing `Wallet.Balance`)
- Age is in years (`int`, consistent with `Alive.Age`)
- Cadence intervals in ticks (consistent with `HiringSystem` at 25t, `PayrollSystem` at 300t)
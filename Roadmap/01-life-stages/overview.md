# Life Stages Overview

A Gene moves through 4 sequential life stages driven by age. Each stage gates access to specific systems, behaviors, and economic roles.

## Stage Lifecycle

```
Birth ──► EDUCATION ──► WORK ──► RETIREMENT ──► ELDER ──► Death
 (0)       (6-18)     (18-65)    (65-80)        (80+)
```

Age thresholds can be tuned via configuration constants; the values below are defaults.

## Stage Determination

A single `LifeStage` component on every Gene entity is the source of truth:

```csharp
enum LifeStage { Child, Student, Worker, Retired, Elder }
```

- `Child` (age 0-5): Pre-education. No economic activity, relies on guardian wallet (future versions).
- `Student` (age 6-17): Education stage. Gains skills, no income.
- `Worker` (age 18-64): Work stage. Existing `JobSeeker`/`Employee` systems activate.
- `Retired` (age 65-79): Retirement stage. Lives on pension/savings.
- `Elder` (age 80+): Terminal stage. No income, declining needs satisfaction, mortality risk.

## Stage Transition System

`StageProgressionSystem` runs at a low cadence (every ~100 ticks). On each pass:

1. Query all `Alive` + `LifeStage` entities
2. For each Gene, compare `Alive.Age` against threshold table
3. If stage must change:
   - Fire transition logic (remove old-stage components, add new-stage components)
   - Emit a transition event for downstream systems (banking, demographics tracking)

### Transition Rules

| Transition | Effects |
|---|---|
| Child → Student | Attach `SkillSet` component with empty skills |
| Student → Worker | Attach `JobSeeker` component; `SkillSet` becomes immutable except for on-the-job gain |
| Worker → Retired | Remove `Employee` (if employed); remove `JobSeeker`; attach `Pensioner`; notify company |
| Retired → Elder | Remove `Pensioner`; attach `Elderly` marker; begin mortality checks |

## Components

| Component | Fields | Notes |
|---|---|---|
| `LifeStage` (record struct) | `LifeStage Stage` | Added at entity creation; never removed |
| `SkillSet` (record struct) | `Dictionary<string, float> Skills` | Added in Education; used by HiringSystem |
| `Pensioner` (record struct) | `float MonthlyPension` | Added at retirement; drives PensionSystem |
| `Elderly` (record struct) | — | Marker tag; triggers MortalitySystem |

## Files

- [`education-stage.md`](education-stage.md) — Education system, skill acquisition
- [`work-stage.md`](work-stage.md) — Work behaviors, integration with existing employment
- [`retirement-stage.md`](retirement-stage.md) — Pension, spending during retirement
- [`elder-stage.md`](elder-stage.md) — End-of-life, mortality mechanics
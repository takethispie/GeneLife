# Work Stage (Age 18-64)

The Work stage is the longest and most economically active phase. Most of the infrastructure already exists — this stage is primarily about integration, adding a `Working` action, and connecting skills to hiring outcomes.

## Stage Entry

- Triggered by `StageProgressionSystem` when `Alive.Age >= 18`
- Components added: `JobSeeker` (with `IsLookingForWork = true`, `DesiredSalary` derived from skills)
- `SkillSet` persists from Education; new skills can be gained on the job (at slower rate)

### Desired Salary from Skills

Instead of `DesiredSalary = 1000f` hardcoded, compute from `SkillSet`:

```csharp
float ComputeDesiredSalary(SkillSet skills) {
    float totalSkill = skills.Skills.Values.Sum();
    return 500f + totalSkill * 1000f; // Range: 500-1500
}
```

## Work Action

### New ActionType Entry

Add `Working` to the `ActionType` enum:

```csharp
enum ActionType { Idle, Sleeping, Showering, Eating, UsingToilet, Working }
```

### Working Behavior

When a Gene is employed (`Employee` component present), the `DecisionSystem` should treat Work as a scheduled obligation, not a needs-driven action.

New component: `WorkSchedule` (record struct)

```csharp
struct WorkSchedule {
    bool IsAtWork;        // Currently on the clock
    float ShiftRemaining; // Ticks left in current shift
    int TickLastShiftStart;
}
```

`WorkSystem` runs every tick:
- If `IsAtWork && ShiftRemaining > 0`: decrement `ShiftRemaining`, set `CurrentAction.Type = Working`, consume Energy at 1.5× rate
- If `IsAtWork && ShiftRemaining <= 0`: set `IsAtWork = false`, set `CurrentAction.Type = Idle`
- If `!IsAtWork` and enough ticks have passed since last shift: start a new shift (`ShiftRemaining = 300t` at 25Hz = ~12 sec)

Working does not restore any need — it consumes Energy. Workers must eat/sleep/shower during off-hours. The `DecisionSystem` should lower the critical threshold for Workers during shifts (only interrupt work for needs below `15f` instead of `30f`).

### Decision System Changes

Extend `DecisionSystem`:
- If entity has `Employee` and `WorkSchedule.IsAtWork`: use lowered critical threshold (15f)
- If entity has `Employee` but `WorkSchedule.IsAtWork == false`: normal threshold (30f) but prefer needs-restoring actions

### Payroll (Existing — No Changes)

`PayrollSystem` already pays `Employee.Salary` to `Wallet.Balance` every 300 ticks. No changes needed unless we want to tie salary to performance/skills (out of scope for V1).

## On-the-Job Skill Gain

Workers slowly improve job-relevant skills. `JobSkillSystem` runs every 500 ticks:
- For employed Genes, slowly increment skills that match their `JobPosting.Requirements`
- Rate: `0.01f` per cadence (very slow compared to education's `0.05f`)

## Hiring Integration

`JobSeekerSystem` already creates `JobSeeker` for age ≥ 18. With StageProgression, it should only run for `LifeStage == Worker`. Remove the age check in `JobSeekerSystem` and replace with stage check:

```csharp
// Old:
if (alive.Age >= 18) { ... }

// New:
if (lifeStage.Stage == LifeStage.Worker && !isEmployed) { ... }
```

## Stage Exit

At age 65, `StageProgressionSystem` transitions to Retired. Effects:
- Remove `Employee` (if employed) — company loses a worker
- Remove `JobSeeker`
- Remove `WorkSchedule`
- Attach `Pensioner` with computed pension
- Housekeeping: deduct from `EmploymentIndex`

## Files to Create/Modify

| File | Action |
|---|---|
| `Genelife/Components/Gen/WorkSchedule.cs` | New |
| `Genelife/Components/Gen/LifeStage.cs` | New (shared across stages) |
| `Genelife/Systems/WorkSystem.cs` | New |
| `Genelife/Systems/JobSkillSystem.cs` | New |
| `Genelife/Enums/ActionType.cs` | Add `Working` |
| `Genelife/Systems/DecisionSystem.cs` | Add work-aware thresholds |
| `Genelife/Systems/JobSeekerSystem.cs` | Replace age check with stage check |

## Acceptance Criteria

- Workers enter Work stage at age 18 with skill-derived salary expectations
- Employed Workers perform `Working` action for ~12 seconds (300t) per shift
- Workers consume extra Energy during work
- DecisionSystem respects work priority (only interrupt for critical needs < 15)
- `JobSeekerSystem` only targets `LifeStage == Worker` entities
- At age 65, Worker transitions to Retired; employment components are cleaned up
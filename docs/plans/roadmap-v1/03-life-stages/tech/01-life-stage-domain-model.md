# L01 — Life stage domain model

**Phase:** 1 — Foundation
**Depends:** E01 (`Money`), E02 (`SimulationClock`)
**Estimate:** 2 sessions
**Status:** Implemented (V1)

## Goal

Define the four-stage lifecycle, the components that represent stage-specific state, and the invariants that make impossible role combinations detectable.

## Context

`Alive.Age` exists but is static. `JobSeekerSystem` uses `Age >= 18` as a proxy for adulthood (`Genelife/Systems/JobSeekerSystem.cs:42`). There is no stage, school, skill level, health or pension state.

## Design

### Stage

```csharp
public enum LifeStage { Education, Work, Retirement, Elderly }

public enum StageTransitionCause
{
    AgedUp, Graduated, Retired, BecameElderly, Death, AdminOverride, Restored
}

public record struct Lifecycle
{
    public LifeStage Stage;
    public long AgeTicks;
    public long StageEnteredTick;
    public StageTransitionCause LastCause;
}
```

`Lifecycle.Stage` is the single source of truth. Age boundaries are policy:

```csharp
public sealed class LifeStagePolicy
{
    public int SchoolStartAge { get; set; } = 6;
    public int WorkStartAge { get; set; } = 18;
    public int RetirementAge { get; set; } = 65;
    public int ElderlyAge { get; set; } = 80;
    public int MaxAge { get; set; } = 110;
}
```

### Stage-specific components

```csharp
public record struct School
{
    public string Name;
    public string District;
    public int Capacity;
    public float Quality;         // 0..1
    public Skill Emphasis;        // bitmask of favoured families
    public Money MonthlyCost;
    public Entity Owner;          // government or private
}

public record struct Enrollment
{
    public Entity School;
    public long EnrolledAtTick;
    public float Attendance;      // 0..1 rolling
    public int MonthsAttended;
}

public record struct SkillProfile
{
    public float Programming, Art, Cooking, Medical, Engineering, Sales;
}

public record struct Health
{
    public float Value;           // 0..100
    public float DeclineRate;
    public long LastCareTick;
}

public record struct WorkSchedule
{
    public Entity Office;
    public bool IsAtWork;
    public int ShiftTicksRemaining;
    public long LastShiftStartTick;
}
```

`PensionAccount`, `WageClaim`, `Household`, `Residence` come from the economy and housing features.

### Invariants

- Every living sim has exactly one `Lifecycle`.
- `Stage` matches the age band, except during a transition apply phase.
- Education: may have `Enrollment`, must not have `Employee`, `JobSeeker` or `Pensioner`.
- Work: may have `Employee` or `JobSeeker`, must not have `Enrollment` or pension payouts.
- Retirement/Elderly: must not have `Employee`, `JobSeeker` or `WorkSchedule`; may receive pension.
- Dead sims have no stage-specific state and belong to no household.

## Tasks

- [ ] Add `LifeStage`, `StageTransitionCause`, `Lifecycle`.
- [ ] Add `School`, `Enrollment`, `SkillProfile`, `Health`, `WorkSchedule`.
- [ ] Add `LifeStagePolicy` with validation (ascending ages, max age).
- [ ] Add a `LifecycleClassifier` used by migration to assign a stage from `Alive.Age`.
- [ ] Backfill every existing sim at world creation/restore.
- [ ] Add debug invariant checks.
- [ ] Add component hygiene tests.

## Files

- new `Genelife/Components/Life/Lifecycle.cs`, `School.cs`, `Enrollment.cs`, `SkillProfile.cs`, `Health.cs`, `WorkSchedule.cs`
- new `Genelife/Enums/LifeStage.cs`, `StageTransitionCause.cs`
- new `Genelife/LifeStagePolicy.cs`
- new `Genelife/LifecycleClassifier.cs`
- `Genelife.UnitTests/Components/ComponentHygieneTests.cs`

## Acceptance

- Every sim created through any path gets a valid `Lifecycle`.
- Classification maps 0–17 → Education, 18–64 → Work, 65–79 → Retirement, 80+ → Elderly.
- Debug invariants catch a student with an employee component.
- Policy validation rejects overlapping or non-ascending bands.
- Components contain no managed collections.

## Testing

- Classification boundary table.
- Policy validation tests.
- Invariant violation tests.
- Backfill test on an existing world.

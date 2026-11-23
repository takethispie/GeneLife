# F05 — Childhood and life stages

**Phase:** 5 — Family
**Depends:** F04, `Lifecycle`, `StageProgressionSystem`, `EnrollmentSystem`, `EducationSystem`
**Estimate:** 1–2 sessions
**Status:** Planned (V4)

## Goal

Let a newborn grow through dependency, school and work using the existing life-stage system, with explicit age gates and no child employment.

## Context

- `LifeStage.Education` already covers ages below `WorkStartAge`.
- `EnrollmentSystem` already requires `Alive.Age >= SchoolStartAge`.
- `StageProgressionSystem` handles age-based transitions.
- `HouseholdRole.Child` and `Tenure.Dependent` exist.
- There is no explicit dependency marker beyond age and role.

## Design

### Childhood definition

A sim is a child when:

- `Lifecycle.Stage == Education`, and
- `Alive.Age < WorkStartAge`.

A sim is a **dependent child** when additionally `Alive.Age < SchoolStartAge` or `Tenure.Dependent` is set.

Do not add a new `LifeStage.Child` in V4a. It would touch every stage count, metric and UI. Use age and role, with a helper:

```csharp
public static bool IsDependentChild(World world, Entity sim, LifeStagePolicy policy);
```

### Age gates

| Age | Behavior |
|---|---|
| 0–5 | at home, no enrollment, no work, guardian pays |
| 6–17 | enrolled in school, education stage, no work |
| 18+ | work stage, job seeker, may remain in the guardian household or move out |

- `EnrollmentSystem` remains the only enrollment path.
- `EducationSystem` remains the only skill-gain path.
- `JobSeekerSystem` must exclude sims below `WorkStartAge`.
- `PayrollSystem` must never pay a sim in the Education stage.
- `PensionSystem` must ignore children.

### Guardian

- The guardian household is the source of residence and child costs.
- Guardian edges are created at birth when the guardian is not a parent.
- If the guardian dies, feature 04 reassigns; until then, the household head is the fallback.

### Transition to adulthood

On reaching `WorkStartAge`:

1. Stage progression moves the sim to `Work`.
2. `JobSeeker` is added if unemployed.
3. `Tenure.Dependent` is cleared.
4. The sim may stay in the household as an adult member or move out under the family housing policy.
5. Parent and sibling edges remain.

## Tasks

- [ ] Add `IsDependentChild` and related helpers.
- [ ] Audit `JobSeekerSystem`, `PayrollSystem` and `PensionSystem` for child exclusion.
- [ ] Ensure `EnrollmentSystem` does not enroll under `SchoolStartAge`.
- [ ] Add the adulthood transition hooks.
- [ ] Add tests for ages 0, 5, 6, 17 and 18.
- [ ] Document that no new life stage is added.

## Files

- new `Genelife/Family/ChildhoodPolicy.cs`
- `Genelife/Systems/EnrollmentSystem.cs`
- `Genelife/Systems/JobSeekerSystem.cs`
- `Genelife/Systems/StageProgressionSystem.cs`
- `Genelife.UnitTests/Family/ChildhoodTests.cs`

## Acceptance

- A newborn is never enrolled or employed.
- A child enrolls at `SchoolStartAge` and not before.
- A child cannot receive a pension or hold a job.
- At `WorkStartAge` the sim becomes a job seeker and clears dependent tenure.
- Parent and sibling edges survive the transition.
- Existing life-stage tests remain green.

## Testing

- Advance a newborn through ages 0, 5, 6, 17, 18 and assert stage, enrollment and work state.
- Assert a child is excluded from payroll and pension.
- Assert the adulthood transition clears `Tenure.Dependent`.
- Replay the same child id and compare transitions.
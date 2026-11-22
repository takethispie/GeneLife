# L03 — Stage progression

**Phase:** 1 — Foundation
**Depends:** L01, L02
**Estimate:** 3 sessions
**Status:** Implemented (V1)

## Goal

Own every lifecycle transition in one idempotent system: education → work → retirement → elderly, with cleanup, component swaps and events.

## Context

Transitions touch employment, housing income, school, pension and health. Spreading this across systems creates impossible states. The repository already uses collect → decide → apply for structural changes (`HiringSystem`), and stage progression must follow the same rule.

## Design

`StageProgressionSystem` order 390, cadence 20, runs before `HiringSystem` (400) so a new worker can be hired on the same tick.

```csharp
var plan = new List<StageChange>();
world.Query(in query, (Entity e, ref Lifecycle life, ref Alive alive) =>
{
    var target = policy.StageFor(alive.Age);
    if (target != life.Stage)
        plan.Add(new StageChange(e, life.Stage, target, cause));
});
foreach (var change in plan)
    Apply(change);
```

### Enter/exit handlers

| Transition | Exit | Enter |
|---|---|---|
| Education → Work | remove `Enrollment`; finalise skills | compute desired salary; add `JobSeeker`; ensure wallet |
| Work → Retirement | remove `Employee` (via separation), `JobSeeker`, `WorkSchedule`; update `EmploymentIndex`; free seat | compute pension; add `RetirementIncome`; set household income change |
| Retirement → Elderly | keep pension; clear work remnants | add elderly decline markers; increase health checks |
| Any → Death | handled by mortality, not this system | — |

Rules:

- Handlers are idempotent: adding an existing component is skipped.
- Employment cleanup uses `EmploymentIndex.Remove` and emits `EmployeeRetired`, not a silent removal.
- A transition never destroys a sim.
- A skipped age band (restore from a much older save) applies every intermediate transition in order.
- `Lifecycle.Stage` is updated last in the apply phase.
- Each transition emits `LifeStageChanged` with from, to, cause and tick.

### Interaction with `JobSeekerSystem`

Replace the age check with a stage check:

```csharp
if (life.Stage == LifeStage.Work && !isEmployed && !isSeeking)
    add JobSeeker;
```

This removes the current hard-coded `WorkingAge` constant.

### Housing signal

Publish `HouseholdIncomeChanged` on work entry/exit/retirement so housing can re-evaluate affordability without polling.

## Tasks

- [ ] Implement the collect/decide/apply pipeline and transition table.
- [ ] Add enter/exit handlers with idempotency.
- [ ] Integrate employment cleanup and `EmployeeRetired`/`EmployeeHired` events.
- [ ] Replace the age check in `JobSeekerSystem` with a stage check.
- [ ] Emit `LifeStageChanged` and `HouseholdIncomeChanged`.
- [ ] Handle multi-band age jumps on restore.
- [ ] Add a debug invariant pass after transitions.
- [ ] Add admin `POST /api/sims/{id}/stage` to force a transition.

## Files

- new `Genelife/Systems/StageProgressionSystem.cs`
- `Genelife/Components/Life/Lifecycle.cs`
- `Genelife/Systems/JobSeekerSystem.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.Messages/Events/LifeStageChanged.cs`
- `Genelife.API/Endpoints/Wolverine/SimEndpoints.cs`

## Acceptance

- Every sim's stage matches its age after a progression pass.
- A worker retiring is removed from `EmploymentIndex` exactly once and frees a seat.
- A student becoming a worker becomes a job seeker on the same tick.
- Repeated progression passes do not duplicate components.
- A restore that jumps several bands applies all transitions in order.
- Admin can force a transition and see the resulting state.

## Testing

- Transition matrix.
- Idempotency test.
- Employment index consistency after retirement.
- Multi-band jump test.
- Forced transition endpoint test.

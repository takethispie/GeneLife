# L07 — Work stage behaviour

**Phase:** 3 — Work
**Depends:** L03, L06, E04, E07
**Estimate:** 2–3 sessions
**Status:** Implemented (V1)

## Goal

Make employment visible in daily behaviour: workers go to their office, work shifts, and must balance work against needs.

## Context

`ActionType` has no `Working` value (`Genelife/Enums/ActionType.cs`). `DecisionSystem` always picks a need-restoring action when a need is critical (`DecisionSystem.cs:200`). Employment exists only as a payroll relationship. There is no schedule or workplace presence.

## Design

### Action

Add `Working` to `ActionType`. It restores nothing and consumes energy at a higher rate.

### WorkScheduleSystem

Order 330, cadence 25 (matching hiring cadence):

- if an employee has no active shift and enough time has passed, start one;
- shift length = policy base × archetype work ethic;
- while on shift: `WorkSchedule.IsAtWork = true`, `CurrentAction.Type = Working`, location set to office;
- on shift end: `IsAtWork = false`, action returns to `Idle`, location returns to home unit;
- if a critical need interrupts work, end the shift early and record an interruption.

Commute is abstract: teleport at shift boundaries. No pathfinding or 2D movement in V1.

### DecisionSystem

While `IsAtWork`:

- critical threshold drops from 30 to 15;
- only Hunger, Bladder and Energy can interrupt;
- interrupting counts as an attendance event (L08).

While off shift:

- normal thresholds;
- workers prefer home-based restoration;
- if homeless or shelter, use shelter modifiers.

### Payroll interaction

Shift attendance does not change the salary in the same cycle; it feeds performance and promotion. This keeps payroll deterministic and avoids hourly wage complexity.

### Guardrails

- Students, retirees and elders never work.
- A worker without an office seat cannot start a shift; this should be impossible and is asserted.
- A player-controlled sim is not auto-scheduled; player input owns its actions.

## Tasks

- [ ] Add `Working` to `ActionType` and all switch statements.
- [ ] Implement `WorkScheduleSystem` with shift start/end and location moves.
- [ ] Add `Working` need effects (energy drain, no restoration).
- [ ] Update `DecisionSystem` work-aware thresholds.
- [ ] Record shift interruptions for attendance.
- [ ] Emit `ShiftStarted`, `ShiftEnded`, `ShiftInterrupted`.
- [ ] Add shift length and interruption policy options.
- [ ] Expose on-shift state in `SimStatus`.

## Files

- `Genelife/Enums/ActionType.cs`
- new `Genelife/Systems/WorkScheduleSystem.cs`
- `Genelife/Systems/DecisionSystem.cs`, `ActionSystem.cs`
- `Genelife/Components/Life/WorkSchedule.cs`
- `Genelife/Components/Gen/Location.cs`
- `Genelife.Messages/Events/Shift*.cs`
- `Genelife.API/DTOs/SimStatus.cs`

## Acceptance

- Employed workers start and end shifts on schedule.
- On-shift sims are located at their office and have `CurrentAction = Working`.
- Needs above the work threshold do not interrupt a shift; critical needs do.
- Interruptions are recorded for attendance.
- Students, retirees and elders never receive a shift.
- Player-controlled sims are not auto-scheduled.
- No pathfinding or per-tick allocation.

## Testing

- Shift lifecycle test.
- Critical-need interruption test.
- Stage gating test.
- Location move/return test.
- Player-controlled exclusion test.

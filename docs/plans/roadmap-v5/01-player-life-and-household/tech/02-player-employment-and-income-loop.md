# P02 — Player employment and income loop

**Phase:** 2 — Economy
**Depends:** P01, V1 `HiringSystem`, `JobSeekerSystem`, `PayrollSystem`, `AttendanceSystem`, `CareerSystem`
**Estimate:** 2 sessions
**Status:** Planned (V5)

## Goal

Let a player find, accept, perform and leave a job under the same labour market, attendance and payroll rules as an NPC.

## Context

NPCs use `JobSeekerSystem` and `HiringSystem` to match skills to postings. `PayrollSystem` pays employees from company accounts. `AttendanceSystem` and `CareerSystem` drive reliability and progression. Player sims are excluded from `WorkScheduleSystem`, so they do not commute or attend automatically.

The player needs explicit actions instead of autonomous decisions. The payroll and company side must not need a player-specific branch.

## Design

### Commands

- `ApplyForJob` with `OfficeNetworkId` and `JobPostingId`.
- `AcceptJobOffer` with `OfferId`.
- `QuitJob` with an optional reason.
- `WorkShift` is not a new action: the existing `UseBuildingIntent` on the player's office sets `ActionType.Working`, and the employment system counts attendance while the player remains at the workplace during the shift window.

### Player employment state

```csharp
public enum EmploymentSource : byte { PlayerApplied, PlayerAcceptedOffer, AdminAssigned }

public record struct PlayerEmployment
{
    public Entity Office;
    public long HiredTick;
    public long LastAttendedTick;
    public int ConsecutiveMissedShifts;
    public EmploymentSource Source;
}
```

`EmploymentSource` distinguishes `PlayerApplied`, `PlayerAcceptedOffer` and `AdminAssigned`.

### Hiring path

1. Player applies at an office entrance.
2. The existing hiring policy evaluates the player with the same skill, availability and seat rules as an NPC.
3. A deterministic decision produces an offer or a rejection with a reason.
4. The player accepts or declines the offer.
5. `EmploymentIndex` is updated in the apply phase.

Player applications do not bypass the seat cap, the posting requirements or the company's hiring freeze.

### Attendance and pay

- The player must be at the office and in `ActionType.Working` during the shift to count attendance.
- Missing shifts follows the existing attendance policy and can lead to warnings or termination.
- Payroll pays the player through the normal `PayrollSystem` and ledger.
- Unpaid wages become arrears or a shortfall under the existing company rules; the player is not paid from nothing.
- Quitting or termination frees the seat and updates the index.

### Career

- Skills and attendance feed the existing career policy.
- A player promotion changes the role and wage through the normal path.
- No player-only wage multiplier or guaranteed promotion.

## Tasks

- [ ] Add `PlayerEmployment` and its index entry.
- [ ] Add `ApplyForJob`, `AcceptJobOffer` and `QuitJob` handlers.
- [ ] Reuse the hiring policy for player applications and offers.
- [ ] Count attendance from `ActionType.Working` at the player's office.
- [ ] Reuse payroll, arrears and termination paths.
- [ ] Add offer expiry and a deterministic rejection reason.
- [ ] Add player employment read fields and admin actions.
- [ ] Add tests for hire, reject, attend, miss, quit and terminate.

## Files

- new `Genelife/Components/Player/PlayerEmployment.cs`
- new `Genelife/Player/PlayerEmploymentService.cs`
- `Genelife/Systems/HiringSystem.cs`
- `Genelife/Systems/PayrollSystem.cs`
- `Genelife/Systems/AttendanceSystem.cs`
- `Genelife/Systems/CareerSystem.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.UnitTests/Player/PlayerEmploymentTests.cs`

## Acceptance

- A player can apply only to an open posting at a reachable office.
- A player who meets the requirements can be hired; a player who does not is rejected with a reason.
- The seat cap and hiring freeze apply to players.
- Working at the office during the shift counts attendance.
- Payroll pays the player from the company account and records the ledger entry.
- A player who misses shifts follows the same attendance consequences as an NPC.
- Quitting frees the seat and removes the player from payroll.

## Testing

- Apply, accept and work a full pay cycle; assert wallet and ledger.
- Apply to a closed posting and assert rejection.
- Fill all seats and assert the player is rejected.
- Miss the attendance threshold and assert warning/termination.
- Quit and assert the seat is available to an NPC.
- Replay the same application sequence and assert identical results.

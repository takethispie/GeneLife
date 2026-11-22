# L08 — Attendance and performance

**Phase:** 3 — Work
**Depends:** L07
**Estimate:** 2 sessions
**Status:** Planned

## Goal

Turn daily work behaviour into a performance record that drives promotions, warnings and separations.

## Context

Payroll pays the same salary regardless of behaviour. Without a performance signal, careers cannot progress and firing has no legitimate trigger.

## Design

```csharp
public record struct EmploymentRecord
{
    public float Attendance;        // 0..1 rolling
    public float Performance;       // 0..1 rolling
    public int TenureCycles;
    public int Warnings;
    public long LastReviewTick;
    public long HiredAtTick;
}
```

Attached to every employee on hire (economy E09).

### Attendance

`AttendanceSystem` order 340, cadence 100 (monthly):

- expected shifts per month = `months × shiftsPerWeek × 4`;
- completed shifts / expected shifts = monthly attendance;
- interrupted shifts count as half;
- rolling attendance = `0.7 × previous + 0.3 × monthly`.

### Performance

Performance combines:

- attendance (40%);
- skill match to the role (30%);
- tenure stability (20%);
- random variance from a seeded stream (10%), representing judgment and luck.

Performance is clamped to `[0, 1]` and updated monthly.

### Warnings and separation

- two consecutive months below 0.35 → warning event;
- three warnings within a year → eligible for firing (economy E09 handles the actual separation);
- performance above 0.75 for a year → eligible for promotion (L09).

No employee is fired directly by this system. It produces eligibility and events; the economy owns the employment relationship.

## Tasks

- [ ] Add `EmploymentRecord` and attach it on hire.
- [ ] Implement monthly attendance calculation from shift events.
- [ ] Implement the performance formula with a seeded variance stream.
- [ ] Emit `AttendanceRecorded`, `PerformanceReview`, `EmployeeWarned`.
- [ ] Expose attendance, performance, tenure and warnings per employee.
- [ ] Add admin actions to adjust or reset a record for scenario testing.
- [ ] Add a debug invariant that every `Employee` has an `EmploymentRecord`.
- [ ] Feed performance into the career and separation systems.

## Files

- new `Genelife/Components/Employment/EmploymentRecord.cs`
- new `Genelife/Systems/AttendanceSystem.cs`
- `Genelife/Systems/HiringSystem.cs`
- `Genelife/Systems/WorkScheduleSystem.cs`
- `Genelife.Messages/Events/Attendance*.cs`, `PerformanceReview.cs`
- `Genelife.API/DTOs/EmployeeStatus.cs`

## Acceptance

- Every employee has exactly one `EmploymentRecord`.
- Missing shifts lower attendance; full attendance keeps it high.
- Performance responds to attendance and skill match.
- Warnings fire at the documented thresholds.
- Records survive save/restore.
- Admin can inspect and adjust records.

## Testing

- Attendance calculation test.
- Rolling average test.
- Performance formula matrix.
- Warning threshold test.
- Restore round-trip test.

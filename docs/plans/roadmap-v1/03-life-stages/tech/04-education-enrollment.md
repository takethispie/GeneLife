# L04 — Education enrollment

**Phase:** 2 — Education
**Depends:** L01, L03
**Estimate:** 2 sessions
**Status:** Planned

## Goal

Create schools, enroll students at school age, and track attendance so education has capacity, cost and variation.

## Context

There are no school entities or enrollment state. The lifecycle can transition a sim to Work, but without education the skill profile is empty and every graduate is identical.

## Design

### Schools

```csharp
public record struct School
{
    public string Name;
    public string District;
    public int Capacity;
    public float Quality;       // 0..1
    public Skill Emphasis;      // favoured skill families
    public Money MonthlyCost;
    public Entity Owner;
    public int Enrolled;
}
```

Ownership: municipality by default; a private school can be created by a company and charge tuition. V1 funds public schools from the government account; private tuition is optional.

### Enrollment

`EnrollmentSystem` order 310, cadence 100:

1. Collect eligible children: `LifeStage.Education`, age ≥ `SchoolStartAge`, no `Enrollment`.
2. Rank schools by district distance (using `Location`/`SpatialIndex`), quality and capacity.
3. Assign the best school with capacity; otherwise mark `Unenrolled`.
4. Create `Enrollment` with attendance 1.0 and emit `StudentEnrolled`.
5. Charge monthly cost to the owner (government or private operator) or tuition to the household.

Attendance:

- attendance is evaluated monthly from health, needs and household stability;
- a homeless or very sick student attends less;
- attendance feeds skill gain and is visible in the admin.

### Unenrolled students

If no school capacity exists:

- they remain in Education but gain skills at a reduced rate (informal learning);
- the admin sees an `UnenrolledStudents` warning;
- the government may build a school through the housing/economy construction pattern or the admin can create one.

## Tasks

- [ ] Add `School` component and `SchoolRepository`.
- [ ] Add `POST/PATCH /api/schools` admin endpoints.
- [ ] Implement `EnrollmentSystem` with capacity and ranking.
- [ ] Add attendance evaluation and `Attendance` updates.
- [ ] Charge school operating cost or tuition through the ledger.
- [ ] Emit `StudentEnrolled`, `StudentUnenrolled`, `SchoolCapacityReached`.
- [ ] Add admin actions to enroll, unenroll and set school quality.
- [ ] Expose enrollment, capacity and attendance in the read model.

## Files

- new `Genelife/Components/Life/School.cs`, `Enrollment.cs`
- new `Genelife.API/Repositories/SchoolRepository.cs`
- new `Genelife/Systems/EnrollmentSystem.cs`
- `Genelife.Messages/Events/StudentEnrolled.cs`
- `Genelife.API/Endpoints/Wolverine/SchoolEndpoints.cs`
- `Genelife.API/DTOs/LifeStageStatus.cs`

## Acceptance

- Every education-stage sim above school age is enrolled or explicitly unenrolled.
- No school exceeds capacity.
- Enrollment prefers higher quality within the same district.
- Attendance changes with health and housing stability.
- School costs post to the correct account.
- Admin can create a school and see enrollment fill.

## Testing

- Capacity and ranking test.
- Unenrolled fallback test.
- Attendance sensitivity test.
- Cost routing test.
- Endpoint authorization test.

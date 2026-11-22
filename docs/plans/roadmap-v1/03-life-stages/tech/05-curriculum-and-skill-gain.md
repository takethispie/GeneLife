# L05 — Curriculum and skill gain

**Phase:** 2 — Education
**Depends:** L04
**Estimate:** 2–3 sessions
**Status:** Planned

## Goal

Produce differentiated graduates: proficiency levels per skill family that depend on school quality, attendance, household background and time.

## Context

`Skill` is a bitmask used for job requirements (`Genelife/Enums/Skill.cs`). It says whether a person has a skill, not how good they are. `JobSeeker.Skills` is hard-coded to `Skill.None` (`JobSeekerSystem.cs`), so hiring match scores are mostly salary-driven.

## Design

```csharp
public record struct SkillProfile
{
    public float Programming, Art, Cooking, Medical, Engineering, Sales;

    public float Get(Skill skill) => skill switch { ... };
    public void Add(Skill skill, float amount) { ... }
}
```

Education runs monthly inside `EducationSystem` order 320, cadence 100.

Per month, for an enrolled student:

```
baseGain = 0.012
quality = school.Quality
attendance = enrollment.Attendance
background = 0.8 + 0.2 × min(1, householdIncome / medianIncome)
emphasis = school.Emphasis has family ? 1.6 : 1.0
gain = baseGain × quality × attendance × background × emphasis
```

- Gains apply to a weighted curriculum: general skills (programming, engineering) and emphasis skills.
- Proficiency caps at 1.0 during education.
- At most two families receive the emphasis multiplier; the rest gain at base.
- No gain while unenrolled beyond a reduced `0.3 × baseGain` informal rate.
- The bitmask is derived: a family is present when proficiency ≥ 0.25.

After 12 years, a typical graduate has one or two families around 0.5–0.8 and the rest 0.1–0.3, depending on quality and attendance. This produces a real matching problem without hard failure.

## Tasks

- [ ] Add `SkillProfile` and conversion to/from the `Skill` bitmask.
- [ ] Implement the monthly curriculum gain in `EducationSystem`.
- [ ] Derive `JobSeeker.Skills` from the profile at graduation.
- [ ] Add school emphasis and household background inputs.
- [ ] Add proficiency caps and deterministic rounding.
- [ ] Emit `SkillMilestoneReached` only when a family crosses 0.25/0.5/0.75.
- [ ] Update `HiringSystem.CalculateMatchScore` to use proficiency, not just the bitmask.
- [ ] Expose skill distribution in the admin.

## Files

- `Genelife/Components/Life/SkillProfile.cs`
- new `Genelife/Systems/EducationSystem.cs`
- `Genelife/Systems/HiringSystem.cs`
- `Genelife/Components/Employment/JobSeeker.cs`
- `Genelife.Messages/Events/SkillMilestoneReached.cs`
- `Genelife.API/DTOs/LifeStageStatus.cs`

## Acceptance

- A student's proficiency rises monthly and caps correctly.
- Higher school quality and attendance produce measurably better graduates.
- Emphasis families end higher than non-emphasis families.
- Graduates have a non-empty skill bitmask.
- Hiring match scores reflect proficiency differences.
- No `Random.Shared`; gains are deterministic per student and month.

## Testing

- 12-year simulation of a single student.
- Quality/attendance/background sensitivity matrix.
- Cap and bitmask conversion tests.
- Hiring score comparison test.
- Determinism test with a fixed seed.

# L06 — Graduation and labour entry

**Phase:** 2 — Education
**Depends:** L03, L05, E04, E07
**Estimate:** 2 sessions
**Status:** Planned

## Goal

Turn an 18-year-old student into a job seeker with a market-relevant skill profile and salary expectation, and remove the hard-coded defaults in the existing job-seeker path.

## Context

`JobSeekerSystem` currently adds every sim aged 18+ as a job seeker with `Skill.None` and `DesiredSalary = 1000` (`Genelife/Systems/JobSeekerSystem.cs:42`). `HiringSystem` scores salary at 60% and skills at 40% (`HiringSystem.cs:289`). That makes graduates interchangeable.

## Design

Graduation is an exit handler of `StageProgressionSystem` (Education → Work) plus a one-shot finalisation:

1. Freeze `SkillProfile`; ensure the `Skill` bitmask is derived.
2. Compute desired salary:

```
skillValue = Σ proficiency
expected = baseSalary[0] + skillValue × salaryPerSkillPoint
householdPressure = 1.0 + 0.1 × (dependents / earners)
desired = expected × householdPressure
```

3. Attach `JobSeeker(IsLookingForWork = true, Skills = mask, DesiredSalary = desired)`.
4. Remove `Enrollment`; clear school link.
5. Set stage to Work and emit `StudentGraduated`.
6. Update household income projection.

`JobSeekerSystem` becomes a safety net only:

- adds a seeker if a Work-stage sim has no `Employee` and no `JobSeeker`;
- never overwrites an existing desired salary;
- no age constant.

Hiring changes:

- `CalculateMatchScore` uses proficiency overlap against `JobPosting.Requirements`;
- salary weight remains, but skill weight rises to 50% when requirements exist;
- a posting with `Skill.None` remains open to all graduates.

Graduates who do not find work within a configured period become eligible for unemployment benefit (L10).

## Tasks

- [ ] Implement the graduation exit handler and salary formula.
- [ ] Remove `WorkingAge` and hard-coded skills/desired salary from `JobSeekerSystem`.
- [ ] Derive the skill mask from proficiency.
- [ ] Update `HiringSystem` match scoring for proficiency.
- [ ] Emit `StudentGraduated` with profile and desired salary.
- [ ] Add `POST /api/sims/{id}/graduate` admin override.
- [ ] Add graduate outcome metrics (employment rate, starting salary).
- [ ] Update existing job-seeker and hiring tests for the new defaults.

## Files

- `Genelife/Systems/StageProgressionSystem.cs`
- `Genelife/Systems/JobSeekerSystem.cs`
- `Genelife/Systems/HiringSystem.cs`
- `Genelife/Components/Employment/JobSeeker.cs`
- `Genelife.Messages/Events/StudentGraduated.cs`
- `Genelife.API/Endpoints/Wolverine/SimEndpoints.cs`
- `Genelife.UnitTests/Systems/JobSeekerSystemTests.cs`, `HiringSystemTests.cs`

## Acceptance

- A graduate receives a skill mask and desired salary derived from their profile.
- Desired salary increases with skills and household dependency pressure.
- `JobSeekerSystem` no longer creates seekers for students, retirees or elders.
- It does not overwrite an existing seeker or desired salary.
- Hiring prefers better skill matches when requirements exist.
- Admin can force graduation and observe the resulting job search.

## Testing

- Graduation profile/salary test.
- Safety-net idempotency test.
- Hiring score sensitivity test.
- Stage-gated seeker test.
- Graduate outcome scenario.

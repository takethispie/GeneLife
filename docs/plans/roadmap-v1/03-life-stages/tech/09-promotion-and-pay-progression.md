# L09 — Promotion and pay progression

**Phase:** 3 — Work
**Depends:** L08, E09
**Estimate:** 2 sessions
**Status:** Planned

## Goal

Let workers build a career: seniority, promotions and salary growth tied to performance, tenure and skills, with realistic limits.

## Context

`Employee.Position` and `Salary` are fixed at hire. There is no notion of seniority, and a worker's income never changes for decades, which makes savings and pensions unrealistic.

## Design

### Seniority ladder

```csharp
public enum Seniority { Associate, Specialist, Senior, Lead, Manager, Director }
```

`Employee` gains `Seniority` and `HiredAtTick`. Salary bands per seniority and role family are configuration.

### CareerSystem

Order 350, cadence 1,200 (yearly):

For each employee:

```
promotionScore = 0.45 × performance
               + 0.25 × tenureFactor
               + 0.20 × skillFit
               + 0.10 × companyHealthFactor
```

- `tenureFactor = min(1, cyclesEmployed / 8)`;
- `skillFit` = proficiency match to the role;
- `companyHealthFactor` is 1.0 for healthy, 0.5 for tight, 0 for arrears/insolvent.

Rules:

- score ≥ 0.72 and a band above exists → promote one step, raise salary to the new band midpoint;
- score ≤ 0.30 for two years → demote one step or flag for separation;
- annual cost-of-living adjustment for all employees when the company is healthy (±3% indexed);
- salary never falls below the legal minimum for the seniority unless the company is restructuring (E15).

Promotions may open a backfill posting for the previous role if the company is healthy and capacity allows. This is the main internal source of new vacancies.

### Bands

```
Associate   0.8–1.2 × base
Specialist  1.1–1.6 × base
Senior      1.5–2.2 × base
Lead        2.0–2.8 × base
Manager     2.4–3.4 × base
Director    3.0–4.5 × base
```

Base salary is per role family, set when the posting is created. Bands are policy, not hard-coded in the system.

## Tasks

- [ ] Add `Seniority` and `HiredAtTick` to `Employee`.
- [ ] Add salary bands and role families to policy.
- [ ] Implement `CareerSystem` with promotion, demotion and indexation.
- [ ] Update `Employee.SalaryPerCycle` and emit `PromotionGranted`, `SalaryAdjusted`, `DemotionApplied`.
- [ ] Create backfill postings through `JobPostingRepository` when appropriate.
- [ ] Add `POST /api/employment/{simId}/promote` for admin scenarios.
- [ ] Expose seniority, tenure and salary history.
- [ ] Ensure payroll and pension contributions use the updated salary next cycle.

## Files

- new `Genelife/Systems/CareerSystem.cs`
- `Genelife/Components/Employment/Employee.cs`
- new `Genelife/CareerPolicy.cs`
- `Genelife.API/Repositories/EmploymentRepository.cs`
- `Genelife.Messages/Events/Promotion*.cs`, `SalaryAdjusted.cs`
- `Genelife.API/Endpoints/Wolverine/EmploymentEndpoints.cs`

## Acceptance

- High performers are promoted at most one step per year.
- Salary moves into the new band and payroll uses it the next cycle.
- Low performers receive demotion or separation eligibility, not silent stagnation.
- Indexation applies only when the company can afford it.
- Promotions can create backfill vacancies without exceeding office capacity.
- Admin can force a promotion and see the result.

## Testing

- Promotion score matrix.
- Band transition test.
- Indexation affordability test.
- Backfill capacity test.
- Payroll-after-promotion test.

# L11 — Retirement transition

**Phase:** 4 — Retirement
**Depends:** L03, L09, E07, E13
**Estimate:** 2 sessions
**Status:** Implemented (V1)

## Goal

Move a worker from employment to retirement atomically, convert their pension statement into income, and free their seat for the labour market.

## Context

`StageProgressionSystem` owns the transition, but retirement has economic consequences that must be consistent: final payroll, pension start, employment cleanup, housing affordability and benefit eligibility.

## Design

At age 65, `StageProgressionSystem` runs the retirement handler:

1. **Final payroll** — pay any earned wages and settle or preserve `WageClaim` arrears.
2. **Employment exit** — remove `Employee`, `WorkSchedule`, `EmploymentRecord`; update `EmploymentIndex`; emit `EmployeeRetired`.
3. **Seat release** — free the employee's seat. The company may repost the role if healthy and capacity allows.
4. **Pension conversion** — read `PensionAccount`; compute monthly pension (L12/L13); add `RetirementIncome`.
5. **Benefit cleanup** — remove any unemployment record; retirees are not job seekers.
6. **Household signal** — emit `HouseholdIncomeChanged`; housing re-evaluates affordability next month.
7. **Stage update** — set `LifeStage.Retirement`; emit `LifeStageChanged` and `Retired`.

```csharp
public record struct RetirementIncome
{
    public Money MonthlyPension;
    public long StartedTick;
    public bool Reduced;
    public string FundReference;
}
```

Retirees keep their home and household role. They may become household head if the previous head dies.

Part-time or phased retirement is out of scope. Retirement is a hard stop at 65 unless the admin overrides the policy age.

## Tasks

- [ ] Implement the retirement handler in `StageProgressionSystem`.
- [ ] Add `RetirementIncome` and the pension conversion call.
- [ ] Ensure final wages and arrears are handled before exit.
- [ ] Remove all work components and update the index exactly once.
- [ ] Repost the freed seat according to company policy.
- [ ] Emit `EmployeeRetired`, `Retired`, `HouseholdIncomeChanged`.
- [ ] Add admin `POST /api/sims/{id}/retire`.
- [ ] Add age-policy override for scenario testing.

## Files

- `Genelife/Systems/StageProgressionSystem.cs`
- new `Genelife/Components/Employment/RetirementIncome.cs`
- `Genelife/Systems/HiringSystem.cs` (seat repost)
- `Genelife.Messages/Events/EmployeeRetired.cs`, `Retired.cs`
- `Genelife.API/Endpoints/Wolverine/SimEndpoints.cs`

## Acceptance

- A 65-year-old worker is no longer an employee or job seeker.
- The seat is freed and may be reposted.
- Pension income starts on the transition tick.
- Final wages and severance-style claims are not lost.
- Household affordability sees the income drop.
- Admin can force retirement without corrupting the index.

## Testing

- Retirement transition test.
- Final wage/arrears test.
- Seat repost test.
- Pension start test.
- Household income signal test.
- Forced retirement endpoint test.

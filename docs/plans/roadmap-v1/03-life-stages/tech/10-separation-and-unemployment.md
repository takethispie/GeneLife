# L10 — Separation and unemployment

**Phase:** 3 — Work
**Depends:** L08, L09, E09, E13
**Estimate:** 2–3 sessions
**Status:** Planned

## Goal

Model leaving a job and the period without one: resignation, firing, unemployment benefit, job search pressure and re-entry.

## Context

Economy E09 implements firing and resignation commands and severance. Life stages must decide when a worker wants to leave and what happens while unemployed. Without benefits, unemployment means immediate homelessness; without pressure, it never ends.

## Design

### Reasons to leave voluntarily

- wages in arrears for 2+ cycles (economy E08);
- performance warnings and demotion (L09);
- better offer elsewhere (future, needs offer comparison);
- retirement (L11);
- health collapse (L14).

For V1, resignation triggers are arrears and repeated demotion. Better-offer poaching is out of scope.

### Unemployment state

A worker without `Employee` and with `JobSeeker.IsLookingForWork = true` is unemployed. Add:

```csharp
public record struct UnemploymentRecord
{
    public long StartedTick;
    public Money BenefitPerCycle;
    public int CyclesClaimed;
    public int MaxCycles;
    public bool Eligible;
}
```

### Benefits

`UnemploymentBenefitSystem` order 525, cadence 300, funded by the government account:

- eligibility requires a contribution history (pension account contributing cycles ≥ 8);
- benefit = 40% of last gross salary, capped;
- duration = 6 pay cycles, extended by 2 for dependents, capped at 10;
- payment debits the government account; if empty, the benefit is unpaid and the shortfall is visible;
- benefit stops on employment, retirement, death or exhaustion.

Job search pressure:

- benefit reduces urgency for the first cycles;
- after benefit exhaustion, the worker lowers desired salary by 10% per cycle down to a floor;
- a worker with arrears or housing risk lowers expectations faster;
- long unemployment increases mortality/health risk through housing and poverty, not a separate mechanic.

### Re-entry

Hiring is unchanged; the worker applies with a lowered desired salary. On hire, `UnemploymentRecord` is removed and `JobSeeker` cleared.

## Tasks

- [ ] Add `UnemploymentRecord` and eligibility rules.
- [ ] Implement `UnemploymentBenefitSystem` with government funding.
- [ ] Add voluntary resignation triggers in the employment review.
- [ ] Implement desired-salary decay during unemployment.
- [ ] Emit `UnemploymentStarted`, `BenefitPaid`, `BenefitExhausted`, `UnemploymentEnded`.
- [ ] Expose unemployment duration, benefit and search status.
- [ ] Add admin actions to grant or deny benefits for scenarios.
- [ ] Coordinate with housing: benefit income counts in household affordability.

## Files

- new `Genelife/Components/Employment/UnemploymentRecord.cs`
- new `Genelife/Systems/UnemploymentBenefitSystem.cs`
- `Genelife.API/Repositories/EmploymentRepository.cs`
- `Genelife.API/Services/CompanyAccountService.cs` (government debit)
- `Genelife.Messages/Events/Unemployment*.cs`, `Benefit*.cs`
- `Genelife.API/Endpoints/Wolverine/EmploymentEndpoints.cs`

## Acceptance

- A separated worker becomes unemployed and eligible or ineligible per contribution history.
- Benefits are paid from the government account, never minted.
- Benefits stop exactly on hire, retirement or exhaustion.
- Desired salary decays after benefit exhaustion and stops at a floor.
- Unemployed workers with dependents receive the extended duration.
- Admin can see and operate the benefit state.

## Testing

- Eligibility matrix.
- Benefit duration and amount test.
- Government shortfall test.
- Salary decay test.
- Re-entry test.
- Interaction test with housing arrears.

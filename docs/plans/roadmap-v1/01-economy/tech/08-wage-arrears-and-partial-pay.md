# E08 — Wage arrears and partial pay

**Phase:** 4 — Payroll
**Depends:** E07
**Estimate:** 2 sessions
**Status:** Implemented (V1)

## Goal

Handle the case where a company cannot meet payroll without inventing money. Workers receive partial pay, the remainder becomes a claim, and recovery or insolvency resolves it.

## Context

Today payroll cannot fail. If it did, a naive implementation would either skip payment silently or drive the balance negative. Both hide the problem from the population and the admin.

## Design

New component on employees:

```csharp
public record struct WageClaim
{
    public Money UnpaidGross;
    public Money UnpaidNet;
    public int CyclesInArrears;
    public long FirstMissedTick;
}
```

Payroll shortfall policy:

1. Cover the pension contribution and tax first if legally required; otherwise treat all obligations as equal for V1 and document the choice in policy options.
2. Pay employees **pro rata** against available cash, rounded down to cents.
3. Record the unpaid remainder as `WageClaim` and `CompanyAccount.Arrears`.
4. Set company state to `Arrears`; emit `WagesDeferred` per employee and `PayrollShortfall` per company.
5. On later cycles, pay current wages first, then oldest arrears. When arrears clear, remove the component and set state back to `Healthy`/`Tight`.

Worker reactions (owned by life stages, E-life 10):

- after two missed cycles, `JobSeeker.IsLookingForWork = true` while still employed;
- after four missed cycles, the worker may resign;
- arrears survive resignation as a claim against the company.

Insolvency: unpaid claims are settled from liquidation in `CompanySolvency`, in priority order. A claim that cannot be settled is written off with `WriteOff` and a visible event.

## Tasks

- [ ] Add `WageClaim` and arrears fields to `CompanyAccount`.
- [ ] Implement the pro-rata payer and claim recorder in the payroll apply phase.
- [ ] Add `WagesDeferred` and `PayrollShortfall` events.
- [ ] Add oldest-first arrears catch-up.
- [ ] Expose arrears per employee, per company and in total.
- [ ] Add `POST /api/companies/{id}/pay-arrears` for admin scenario control.
- [ ] Add an alert when total arrears exceed a configured share of payroll.
- [ ] Coordinate with E-life: no resignation logic in this step, only the claim data.

## Files

- new `Genelife/Components/Employment/WageClaim.cs`
- `Genelife/Systems/PayrollSystem.cs`
- `Genelife/Components/Company/CompanyAccount.cs`
- `Genelife.Messages/Events/WagesDeferred.cs`, `PayrollShortfall.cs`
- `Genelife.API/DTOs/EmployeeStatus.cs`, `CompanyStatus.cs`
- `Genelife.API/Endpoints/Wolverine/CompanyEndpoints.cs`

## Acceptance

- A company with 50% of payroll cash pays every employee 50% and records the other 50% as claims.
- The company balance never goes negative from payroll.
- When cash returns, arrears are paid oldest first and claims shrink to zero.
- The admin can see total and per-employee arrears.
- Written-off claims are ledgered and never counted as paid.
- Conservation holds: paid + unpaid = gross obligations.

## Testing

- Pro-rata rounding test with 3 employees and an odd cent remainder.
- Arrears catch-up over multiple cycles.
- Claim survives employee resignation.
- Write-off ledger test.

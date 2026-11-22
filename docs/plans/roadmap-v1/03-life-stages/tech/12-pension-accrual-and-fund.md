# L12 — Pension accrual and fund

**Phase:** 4 — Retirement
**Depends:** E13, L11
**Estimate:** 2 sessions
**Status:** Planned

## Goal

Define how pension entitlement accrues over a working life, how the fund is managed, and how individual statements relate to the pooled fund.

## Context

E13 already owns `PensionFund`, `PensionAccount` and the payroll deductions that credit them. This step owns the accrual rules, statement semantics, fund yield and funding-ratio policy — the life-stage view of the same money.

## Design

### Accrual

Each pay cycle, for each employee:

```
employeeContribution = gross × employeeRate      // default 5%
employerContribution = gross × employerRate      // default 5%
account.EmployeeContributions += employeeContribution
account.EmployerContributions += employerContribution
account.ContributingCycles += 1
fund.Balance += both contributions
```

E13 implements the movement; this step validates it and exposes statements.

### Benefit formula

At retirement:

```
annualPension = (employeeContrib + employerContrib) / annuityFactor
monthlyPension = annualPension / 12
annuityFactor = expectedYearsInRetirement × adjustment
```

- `expectedYearsInRetirement` is derived from the policy retirement and elderly ages, with a minimum.
- A configured minimum pension applies if the account is small but the worker contributed for the minimum number of cycles.
- No pension if contributing cycles < 8; the retiree relies on savings and the means-tested top-up.

### Fund management

- The fund earns a configured annual yield (default 2%) credited at the year boundary.
- Yield is distributed proportionally to account balances, not to cash.
- The funding ratio = fund balance / sum of projected liabilities.
- If the ratio falls below 0.8, contributions rise by a policy step and indexation pauses.
- If below 0.5, the government top-up is required; if unavailable, payouts are reduced.

### Statements

The admin and each sim can see:

- contributions by year;
- projected monthly pension;
- retirement age;
- fund reference.

### Indexation

Pensions are indexed annually by inflation (a configured rate) only when the funding ratio is at least 1.0. Never index while the fund is short.

## Tasks

- [ ] Add accrual validation and statement read model.
- [ ] Implement the benefit formula and minimum pension rule.
- [ ] Add annual fund yield distribution.
- [ ] Add funding-ratio calculation and policy thresholds.
- [ ] Emit `PensionAccrued`, `PensionProjectionUpdated`, `PensionFundYieldCredited`.
- [ ] Add contribution-rate and yield policy options.
- [ ] Expose per-sim and aggregate statements in the admin.
- [ ] Add a reconciliation test between fund and statements after yield.

## Files

- `Genelife/Components/Employment/PensionAccount.cs`
- `Genelife/Components/Finance/PensionFund.cs`
- new `Genelife/PensionPolicy.cs`
- new `Genelife.API/Services/PensionService.cs`
- `Genelife.Messages/Events/PensionAccrued.cs`
- `Genelife.API/DTOs/LifeStageStatus.cs`

## Acceptance

- Every contributing employee's statement grows by the correct amounts.
- Fund balance equals the sum of statements before payout, including yield.
- Benefit formula produces a plausible replacement rate (30–60% of final salary).
- Minimum pension applies only to eligible contributors.
- Funding ratio drives indexation and contribution policy.
- Statements reconcile with payroll deductions.

## Testing

- Accrual arithmetic test.
- Benefit formula matrix.
- Minimum pension test.
- Yield distribution and reconciliation test.
- Funding-ratio policy test.

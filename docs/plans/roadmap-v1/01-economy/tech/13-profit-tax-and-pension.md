# E13 — Profit, tax and pension contributions

**Phase:** 7 — Fiscal layer
**Depends:** E07, E12
**Estimate:** 2 sessions
**Status:** Implemented (V1)

## Goal

Add the public layer: corporate profit tax, government account and funded pension contributions. Every deduction has a destination; no money disappears.

## Context

Payroll already withholds employee pension and income tax (E07), but there is no fund to receive them and no profit calculation for companies. Pensions cannot be paid from an empty promise; they must be funded during working life.

## Design

### Accounts

Singleton entities with `NetworkId` reserved at world creation:

```csharp
public record struct GovernmentAccount
{
    public Money Balance;
    public Money IncomeTaxCollected;
    public Money CorporateTaxCollected;
    public Money BenefitsPaid;
    public Money PensionsSubsidised;
}

public record struct PensionFund
{
    public Money Balance;
    public Money ContributionsThisCycle;
    public Money PayoutsThisCycle;
    public Money Shortfall;
    public float TargetFundingRatio;
}
```

### Corporate tax

`CorporateTaxSystem` order 535, cadence 300, after payroll:

```
profit = RevenueLastCycle − WagesLastCycle − OfficeCosts − InterestPaid
taxable = max(0, profit − LossCarryForward)
tax = taxable × CorporateTaxRate        // default 15%
```

- Debit company account; if cash is insufficient, record `TaxArrears` and debt.
- Credit `GovernmentAccount`; ledger `TaxPaid`.
- Negative profit accumulates as `LossCarryForward`, capped at three years.
- No tax while the company is in `Arrears` or `Insolvent`; the liability is recorded instead.

### Pension contributions

Employee and employer contributions from E07 are credited to `PensionFund` at payroll time. Each employed sim gets a `PensionAccount`:

```csharp
public record struct PensionAccount
{
    public Money EmployeeContributions;
    public Money EmployerContributions;
    public int ContributingCycles;
    public long LastContributionTick;
}
```

`PensionFund` is the pooled asset; `PensionAccount` is the individual statement used to compute the retirement benefit. Both must agree: fund balance equals sum of accounts while no payouts have occurred.

### Government spending

V1 uses the government account for:

- unemployment benefit (E-life 10);
- pension top-up when the fund is short (E-life 13);
- shelter operation (housing).

Every payment is ledgered and appears in the admin fiscal panel.

## Tasks

- [ ] Add `GovernmentAccount` and `PensionFund` singletons, created with the world.
- [ ] Add `PensionAccount` and credit it during payroll.
- [ ] Implement `CorporateTaxSystem` and loss carry-forward.
- [ ] Add corporate tax, income tax, benefit and pension totals to the read model.
- [ ] Add `GET /api/economy/fiscal` and a Fiscal tab in the admin UI.
- [ ] Add policy options for tax rate, loss carry-forward years and pension rates.
- [ ] Add alerts for tax arrears and pension funding ratio below target.
- [ ] Ensure tax and pension sums reconcile with payroll deductions.

## Files

- new `Genelife/Components/Finance/GovernmentAccount.cs`, `PensionFund.cs`
- new `Genelife/Components/Employment/PensionAccount.cs`
- new `Genelife/Systems/CorporateTaxSystem.cs`
- `Genelife/Systems/PayrollSystem.cs`
- new `Genelife/FiscalPolicy.cs`
- `Genelife.API/DTOs/FiscalStatus.cs`
- `Genelife.API/Endpoints/Wolverine/EconomyEndpoints.cs`

## Acceptance

- Corporate tax is charged only on positive taxable profit and only when cash exists.
- Tax arrears are visible and never silently forgiven.
- Pension fund balance equals the sum of individual pension accounts before payouts.
- Government account movements reconcile with taxes collected and benefits paid.
- Admin can see all fiscal flows for the current year.
- Payroll tax and pension deductions match fund/government credits exactly.

## Testing

- Profit/tax calculation with loss carry-forward.
- Insufficient cash tax arrears test.
- Pension statement vs fund reconciliation test.
- Fiscal read-model test.

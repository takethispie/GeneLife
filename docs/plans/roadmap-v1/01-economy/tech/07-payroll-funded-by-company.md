# E07 — Company-funded payroll

**Phase:** 4 — Payroll
**Depends:** E05, E06
**Estimate:** 2–3 sessions
**Status:** Implemented (V1)

## Goal

Rewrite `PayrollSystem` so wages are paid by the employer from its account, with tax and pension withholding, exact ledger entries and a deterministic apply phase.

## Context

`PayrollSystem` queries `Employee + Wallet` and adds `Salary` (`Genelife/Systems/PayrollSystem.cs:35`). It does not know which company employs the worker, cannot fail, and creates money. `EmploymentIndex` already maps employee → company and company → employees (`Genelife/Components/Employment/EmploymentIndex.cs`), so employer lookup is O(1).

## Design

Per pay cycle (300 ticks):

1. **Collect** — snapshot employees and wallets; group employees by company via `EmploymentIndex`.
2. **Decide** — per company compute:
   - `gross = Σ Employee.SalaryPerCycle`
   - `employeePension = gross × EmployeePensionRate` (default 5%)
   - `employerPension = gross × EmployerPensionRate` (default 5%)
   - `incomeTax = gross × IncomeTaxRate` (default 12%)
   - `net = gross − employeePension − incomeTax`
   - `cashNeeded = net + employerPension`
3. **Fund** — attempt to debit the company account `cashNeeded`.
4. **Apply** — credit each employee wallet `net`; credit pension fund contributions; credit government tax; write ledger entries; emit `WagesPaid`.
5. **Shortfall** — if the account cannot cover the full amount, hand off to E08 instead of paying partially in this step.

No structural changes and no I/O in the tick. Ledger entries go through `FinanceLedger`.

Payroll is gross-per-cycle. The `Employee.Salary` rename to `SalaryPerCycle` makes the period explicit.

## Tasks

- [ ] Rename `Employee.Salary` to `SalaryPerCycle` and update `EmployeeStatus`, `HiringSystem`, DTOs and UI.
- [ ] Rewrite `PayrollSystem` into collect/decide/apply with `CompanyAccountService`.
- [ ] Add `PayrollPolicy` options: tax rate, employee pension rate, employer pension rate, rounding mode.
- [ ] Withhold and route tax and pension contributions through the ledger.
- [ ] Emit `WagesPaid` per employee and `PayrollCycleCompleted` per company.
- [ ] Keep system order 500 and cadence 300 unless the clock is reconfigured.
- [ ] Add payroll totals to `CompanyAccount` and the admin economy read model.
- [ ] Ensure employees without a wallet get one created in the apply phase, not during the query.

## Files

- `Genelife/Systems/PayrollSystem.cs`
- `Genelife/Components/Employment/Employee.cs`
- `Genelife.API/Services/CompanyAccountService.cs`
- new `Genelife/PayrollPolicy.cs`
- `Genelife.Messages/Events/WagesPaid.cs`
- `Genelife.API/Services/SimulationEngine.cs` (wiring/options)
- `Genelife.UnitTests/Systems/PayrollSystemTests.cs`

## Contracts

`WagesPaid(ulong CompanyId, ulong SimId, long GrossCents, long NetCents, long TaxCents, long PensionCents, long Tick)`

## Acceptance

- Wages debit exactly the employer account and credit exactly the employee wallets.
- Gross equals net + employee pension + income tax for every employee.
- Company balance after payroll equals previous balance minus net payroll minus employer pension.
- No money is created; the ledger conservation test passes.
- A company without funds does not pay and does not go negative; it enters the E08 path.
- Payroll at 300 ticks remains compatible with existing tests.

## Testing

- Payroll arithmetic test with rounding cases.
- Multi-company payroll test with separate balances.
- Insufficient-funds handoff test.
- 100-cycle ledger reconciliation test.

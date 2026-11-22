# E15 — Bankruptcy and restructuring

**Phase:** 8 — Resilience
**Depends:** E13, E14
**Estimate:** 2–3 sessions
**Status:** Implemented (V1)

## Goal

Make company failure a controlled process: distress is detected, recovery is attempted, and liquidation settles obligations in a defined order without corrupting employment or housing.

## Context

`CompanySolvency` state exists from E05 but nothing transitions it. Deleting a company today removes employees from `EmploymentIndex` (`CompanyRepository.Delete`), but there is no accounting, no notice and no asset handling.

## Design

`CompanySolvencySystem` order 540, cadence 300, evaluates each company after payroll and tax:

| State | Enter when | Exit when |
|---|---|---|
| Healthy | cash ≥ obligations + reserve | cash < obligations + reserve |
| Tight | cash covers wages, not reserve | cash ≥ reserve or cash < wages |
| Arrears | cash < wages | arrears = 0 |
| Insolvent | arrears > 0 for 3 cycles or loan default | restructuring succeeds |
| Bankrupt | no recovery after N cycles | terminal |

### Restructuring (Insolvent)

Attempt in order:

1. Close the least-utilised office if it has no employees.
2. Reduce open seats and freeze hiring.
3. Cut salaries by a policy percentage for all employees, recording the new contract.
4. Make redundant employees beyond a target headcount, paying severance or recording claims.

Each action is an event and appears in the admin timeline. If cash becomes non-negative and arrears clear, state returns to `Tight`.

### Liquidation (Bankrupt)

1. Stop all postings; reject pending applications.
2. Sell remaining office leases and property assets to solvent companies at a discount; unsold assets are written off.
3. Liquidate remaining cash and sale proceeds through the waterfall:
   - employee wage and severance claims;
   - tax arrears and government account;
   - secured loan principal;
   - unsecured loan interest;
   - remaining unsecured creditors.
4. Separate all employees, each receiving a claim if unpaid.
5. Flag the company `Bankrupt`; keep the entity for history and admin display.
6. Emit `CompanyLiquidated` with a settlement report.

Never destroy the company entity automatically. History matters for the admin and for population trust in the labour market.

## Tasks

- [ ] Implement the solvency state machine in `CompanySolvencySystem`.
- [ ] Add restructuring actions with policy thresholds.
- [ ] Implement the liquidation waterfall as a pure planner plus apply phase.
- [ ] Integrate with payroll arrears, credit defaults and employee separation.
- [ ] Close postings and free office seats safely.
- [ ] Emit `CompanyDistressed`, `CompanyRestructured`, `CompanyLiquidated`, `LiquidationPayout`.
- [ ] Add admin endpoints to trigger restructuring or liquidation for scenarios.
- [ ] Show state history and settlement report in the economy page.
- [ ] Guard invariants: no hires, no loans, no dividends while insolvent.

## Files

- new `Genelife/Systems/CompanySolvencySystem.cs`
- new `Genelife.API/Services/LiquidationPlanner.cs`
- `Genelife/Components/Company/CompanyAccount.cs`
- `Genelife.API/Repositories/CompanyRepository.cs`
- `Genelife.Messages/Events/Company*.cs`
- `Genelife.API/Endpoints/Wolverine/CompanyEndpoints.cs`

## Acceptance

- A company that cannot cover wages for three cycles enters `Insolvent`.
- Restructuring can return a company to `Tight` without liquidation.
- Liquidation pays claims in the defined priority order and never exceeds available assets.
- Employees are separated exactly once and seats are freed.
- Postings and applications are closed cleanly.
- The admin sees the full settlement report and ledger trail.

## Testing

- State machine transition matrix.
- Restructuring scenario test.
- Waterfall priority test with insufficient assets.
- Idempotent liquidation test.
- Integration test: distressed company over 10 cycles.

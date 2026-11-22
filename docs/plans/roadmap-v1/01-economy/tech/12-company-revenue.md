# E12 — Company revenue

**Phase:** 6 — Demand
**Depends:** E11
**Estimate:** 1–2 sessions
**Status:** Planned

## Goal

Turn consumer purchases into company revenue on the same pay cycle, closing the loop between wages and spending.

## Context

`ConsumerSpendingSystem` records purchases attributed to companies. Companies need that money before payroll so they can fund the next cycle. No revenue system exists today.

## Design

`CompanyRevenueSystem` at order 450, cadence 300 (quarterly), runs **before** payroll at order 500.

Inputs: pending `ConsumerSpendBooked` records accumulated since the last settlement, grouped by company and category.

Per company:

- `revenue = Σ purchase amounts`
- credit `CompanyAccount.Balance`
- set `RevenueLastCycle`
- ledger `CompanyRevenue` with a per-category breakdown in `Memo`
- emit `CompanyRevenueBooked`

There is no cost-of-goods leakage in V1: the full consumer price is company revenue. Profit is revenue minus wages, office cost, tax and loan interest. This keeps the money loop closed. A future supplier layer can introduce COGS without changing the revenue contract.

If a company is bankrupt, its pending revenue is still credited to the estate, then distributed in liquidation (E15). Never drop it silently.

Attribution fairness:

- 70% weight by employed headcount;
- 30% weight by office capacity, so a company that built capacity can capture demand before it hires;
- weights recomputed at settlement, not per purchase;
- seeded random per world.

## Tasks

- [ ] Add `PendingRevenue` buffer on `CompanyAccountService` fed by spending events.
- [ ] Implement `CompanyRevenueSystem` with collect/decide/apply.
- [ ] Credit accounts, write ledger entries and emit `CompanyRevenueBooked`.
- [ ] Order revenue before payroll and document the dependency in the registry table.
- [ ] Add revenue by category to the read model.
- [ ] Handle bankrupt-company revenue through the estate pipeline.
- [ ] Add a check that settled revenue equals `Σ ConsumerSpendBooked` for the cycle.

## Files

- new `Genelife/Systems/CompanyRevenueSystem.cs`
- `Genelife.API/Services/CompanyAccountService.cs`
- `Genelife.Messages/Events/CompanyRevenueBooked.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.API/DTOs/CompanyStatus.cs`

## Acceptance

- Revenue lands in company accounts after the settlement tick.
- Sum of revenue equals sum of consumer spending for the same window.
- Revenue arrives before wages are due.
- Category breakdown is visible per company.
- Bankrupt-company revenue is not lost.
- Ledger conservation holds across spending + revenue.

## Testing

- One-cycle spending → revenue settlement test.
- Multi-company attribution test.
- Revenue-before-payroll ordering test.
- Bankrupt revenue routing test.
- Sum equality test.

# Economy — Summary

**Folder:** `01-economy/`
**Depends on:** existing `Office`, `JobPosting`, `OpenPosition`, `JobApplication`, `Employee`, `HiringSystem`, `PayrollSystem`, `Wallet`
**Blocks:** housing (rent needs income), life stages (work and pensions need accounts)

## Objective

Turn the current labour-market skeleton into a closed, auditable economy:

- companies have accounts and must fund payroll;
- offices and job postings are administrable resources with capacity and cost;
- hiring, wages, firing and severance produce ledger entries;
- consumer spending routes money back to companies;
- insolvency is a real state with consequences;
- the admin UI can inspect and operate every part.

## Starting point

| Piece | Today | Gap |
|---|---|---|
| `Office` | component, created in tests only | no API, no operating cost, capacity not enforced |
| `JobPosting` / `OpenPosition` | components, created in tests only | no API; `CreateJobPosting` message has no handler |
| `HiringSystem` | matches seekers, hires, updates `EmploymentIndex` | no recruitment cost, no affordability check, no events |
| `PayrollSystem` | adds `Employee.Salary` to `Wallet` every 300 ticks | money appears from nowhere; no company debit; no arrears |
| Company money | none | no account, no revenue, no insolvency |
| Money type | `float` | rounding drift, no ledger reconciliation |

## Deliverables

1. `Money` value type in cents and a migrated `Wallet`.
2. `SimulationClock` with pay cycles aligned to the existing 300-tick payroll cadence.
3. Office CRUD and capacity enforcement.
4. Job posting CRUD and seat invariants.
5. `CompanyAccount` with seed capital and reserved cash.
6. Append-only ledger with idempotent transaction ids.
7. Company-funded payroll with partial payment and arrears.
8. Recruitment cost, firing cost and vacancy recreation.
9. Household-aware consumer spending attributed to companies.
10. Company revenue, margin and profit.
11. Corporate tax and pension contributions into a government/pension fund.
12. Credit lines, interest and repayment.
13. Bankruptcy and restructuring.
14. Economy metrics and health checks.
15. Persistence, schema migration and restore reconciliation.
16. Admin economy page with operational actions.
17. Household savings accounts and a funded interest pool.

## Architecture

```
        ┌────────────────────┐
        │  CompanyAccount    │◄──── revenue ──── ConsumerSpending
        │  balance, reserve  │
        └──────┬─────────────┘
               │ payroll, costs, tax, loan
               ▼
        ┌────────────────────┐        ┌──────────────┐
        │  FinanceLedger     │◄──────►│  Marten      │
        │  append-only       │        │  events      │
        └────────────────────┘        └──────────────┘
               ▲
               │ wages, arrears, severance
        ┌──────┴─────────────┐
        │  Wallet / Savings  │
        └────────────────────┘
```

## Tech steps

| # | Step | Phase |
|---|---|---|
| 01 | [Money type and wallet migration](tech/01-domain-model-and-money.md) | Foundation |
| 02 | [Time and pay cycles](tech/02-time-and-pay-cycles.md) | Foundation |
| 03 | [Office administration](tech/03-office-administration.md) | Labour market |
| 04 | [Job posting administration](tech/04-job-posting-administration.md) | Labour market |
| 05 | [Company accounts](tech/05-company-accounts.md) | Money |
| 06 | [Ledger and transactions](tech/06-ledger-and-transactions.md) | Money |
| 07 | [Company-funded payroll](tech/07-payroll-funded-by-company.md) | Payroll |
| 08 | [Wage arrears and partial pay](tech/08-wage-arrears-and-partial-pay.md) | Payroll |
| 09 | [Hiring costs and separation](tech/09-hiring-costs-and-separation.md) | Labour market |
| 10 | [Office capacity and utilisation](tech/10-office-capacity.md) | Labour market |
| 11 | [Consumer spending](tech/11-consumer-spending.md) | Demand |
| 12 | [Company revenue](tech/12-company-revenue.md) | Demand |
| 13 | [Profit, tax and pension contributions](tech/13-profit-tax-and-pension.md) | Demand |
| 14 | [Credit and loans](tech/14-credit-and-loans.md) | Resilience |
| 15 | [Bankruptcy and restructuring](tech/15-bankruptcy-and-restructuring.md) | Resilience |
| 16 | [Economy metrics](tech/16-economy-metrics.md) | Observability |
| 17 | [Commands and events](tech/17-command-and-event-contracts.md) | Plumbing |
| 18 | [System wiring and determinism](tech/18-system-wiring-and-determinism.md) | Plumbing |
| 19 | [Persistence and migration](tech/19-persistence-and-migration.md) | Plumbing |
| 20 | [Admin API and UI](tech/20-admin-api-and-ui.md) | Admin |
| 21 | [Testing and acceptance](tech/21-testing-and-acceptance.md) | Verification |
| 22 | [Savings and banking](tech/22-savings-and-banking.md) | Money |

Step numbers are stable identifiers, not strict execution order. The **Phase** column defines delivery order: E22 ships with the money phase, before demand (E11) and payroll-dependent housing work.

## Business reading

- [Why a closed economy](business/01-closed-money-loop.md)
- [How the labour market works](business/02-labour-market.md)
- [Company solvency and failure](business/03-company-solvency.md)
- [Balance targets and levers](business/04-balance-targets.md)

## Definition of done

- A company cannot pay wages it cannot afford; unpaid wages become visible arrears.
- Every cent moved by wages, spending, rent, tax or loans has a ledger entry with a stable id.
- Offices and postings can be created, closed and inspected from the admin UI.
- A 10-year run keeps money supply roughly stable, unemployment between 4% and 8%, and at least one company solvent.
- Snapshot restore reproduces company accounts, postings, seats, applications and employment.
- `dotnet test` green, including new payroll, ledger, revenue and insolvency tests.

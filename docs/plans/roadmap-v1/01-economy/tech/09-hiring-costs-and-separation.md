# E09 — Hiring costs and separation

**Phase:** 5 — Labour market economics
**Depends:** E07
**Estimate:** 2 sessions
**Status:** Planned

## Goal

Make hiring and firing cost money, so workforce decisions are economic rather than free. Preserve the existing collect → decide → apply pipeline.

## Context

`HiringSystem.HireApplicant` (`Genelife/Systems/HiringSystem.cs:265`) adds `Employee`, a wallet and updates `EmploymentIndex`. There is no cost, no affordability check, and `EmployeeFired` exists as an event with no producer.

## Design

- **Recruitment cost** — one-time charge when a seat is filled. Default: 25% of one pay cycle salary, minimum one unit. Configurable per company size.
- **Affordability gate** — in the decide phase, a company with `CompanySolvency.Insolvent` or `Bankrupt`, or with arrears, cannot hire. A company that cannot pay the recruitment cost skips the candidate and leaves the seat open.
- **Severance** — when a company initiates separation, it owes `SeveranceCycles × SalaryPerCycle` (default 2). Unpaid severance becomes a `WageClaim` with kind `Severance`.
- **Vacancy recreation** — a separation frees an `OpenPosition`; the company may repost depending on its state. Healthy companies repost automatically after a configurable delay; tight companies do not.
- **Events** — `EmployeeHired` (exists), `RecruitmentFeeCharged`, `EmployeeFired`, `SeverancePaid`, `EmployeeResigned`, `VacancyReopened`.

The hire apply phase order is: validate → charge recruitment → add `Employee` → remove `JobSeeker` → update index → emit events. If the charge fails, no hire occurs.

## Tasks

- [ ] Add cost policy options: recruitment rate, severance cycles, repost delay.
- [ ] Extend `HiringSystem` decide phase with solvency and affordability checks.
- [ ] Collector for separations and firings with a plan object (mirror `HirePlan`).
- [ ] Charge recruitment fees through `CompanyAccountService` and the ledger.
- [ ] Implement `FireEmployee` and `ResignEmployee` commands.
- [ ] Emit all separation events; make `CompanyProjection` consume them.
- [ ] Recreate vacancies according to company state and policy.
- [ ] Expose recruitment and severance totals in the economy read model.

## Files

- `Genelife/Systems/HiringSystem.cs`
- new `Genelife/Systems/SeparationSystem.cs` (or an apply service used by commands)
- new `Genelife.API/Repositories/EmploymentRepository.cs`
- `Genelife.Messages/Commands/FireEmployee.cs`
- `Genelife.Messages/Events/{RecruitmentFeeCharged,SeverancePaid,EmployeeResigned}.cs`
- `Genelife.API/Endpoints/Wolverine/EmploymentEndpoints.cs`

## Contracts

| Method | Route | Semantics |
|---|---|---|
| POST | `/api/employment/{simId}/fire` | company-initiated separation |
| POST | `/api/employment/{simId}/hire` | force hire into an existing posting |
| POST | `/api/job-postings/{id}/reopen` | recreate a vacancy |

## Acceptance

- Hiring debits the company and produces a ledger entry.
- An insolvent company cannot hire even with open seats.
- A failed recruitment charge leaves no `Employee` component and no index entry.
- Firing pays severance or records a claim.
- A separation frees exactly one seat and may repost it.
- `EmploymentIndex` and `CompanyDocument.EmployeeCount` stay consistent.

## Testing

- Hire-with-fee accounting test.
- Insolvent hiring rejection test.
- Fire with and without severance cash.
- Vacancy recreation policy test.
- Index and projection consistency test.

# E05 — Company accounts

**Phase:** 3 — Money
**Depends:** E01, E03
**Estimate:** 1–2 sessions
**Status:** Implemented (V1)

## Goal

Give every company an account that holds cash, tracks recurring obligations and exposes solvency state. Payroll, revenue, tax and office costs move money through this account.

## Context

`CompanyName` is the only company component (`Genelife/Components/Company/CompanyName.cs`), and `CompanyDocument` stores just name and employee count. Wages currently appear from nowhere, so a company can never be short of money.

## Design

```csharp
public enum CompanySolvency { Healthy, Tight, Arrears, Insolvent, Bankrupt }

public record struct CompanyAccount
{
    public Money Balance;
    public Money ReservedForPayroll;
    public Money RevenueLastCycle;
    public Money WagesLastCycle;
    public Money CostsLastCycle;
    public Money Arrears;
    public CompanySolvency State;
    public long StateChangedAtTick;
}
```

Invariants:

- `Balance` never goes negative through normal operations. A forced negative balance is allowed only by an explicit credit or insolvency operation that records debt.
- `ReservedForPayroll` is a planning figure, not a separate pot. It is recomputed when postings or salaries change.
- `Arrears` is the unpaid wage total, never silently cleared.

`CompanyAccountService` centralises mutations:

```csharp
bool TryDebit(Entity company, Money amount, LedgerKind kind);
void Credit(Entity company, Money amount, LedgerKind kind);
void SetReserve(Entity company, Money amount);
```

`CompanyRepository.Add` seeds `InitialCompanyCapital` (default 10,000 units) and attaches `CompanyAccount`. Seeding is a ledger source event `CapitalInjected`.

## Tasks

- [ ] Add `CompanyAccount` and `CompanySolvency`.
- [ ] Add `CompanyAccountService`; all balance changes go through it.
- [ ] Seed accounts in `CompanyRepository.Add`; make seed amount configurable.
- [ ] Track office operating costs in `CostsLastCycle` (charge lands in E13/E15).
- [ ] Add `CompanyAccountStatus` DTO: balance, reserve, arrears, revenue, wages, state, state age.
- [ ] Extend `CompanyDocument` and `CompanyProjection` with money and state fields.
- [ ] Add `GET /api/companies/{id}/account` and include the account in `/api/data`.
- [ ] Add admin `POST /api/companies/{id}/capital` for explicit capital injection.

## Files

- new `Genelife/Components/Company/CompanyAccount.cs`
- new `Genelife.API/Services/CompanyAccountService.cs`
- `Genelife.API/Repositories/CompanyRepository.cs`
- `Genelife.API/Models/CompanyDocument.cs`, `Genelife.API/Projections/CompanyProjection.cs`
- `Genelife.API/DTOs/CompanyStatus.cs`
- `Genelife.API/Endpoints/Wolverine/CompanyEndpoints.cs`
- `Genelife.Web/Models/WorldData.cs`

## Acceptance

- Every company has an account immediately after creation.
- Account balance equals the sum of its ledger entries at all times.
- A debit larger than balance fails and leaves the balance unchanged.
- Bankruptcy state is visible to the admin and blocks new postings.
- `/api/data` exposes company money without a second round trip.

## Testing

- Debit/credit invariant tests.
- Seed capital ledger test.
- Account read-model projection test.
- Admin capital injection endpoint test.

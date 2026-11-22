# E20 — Admin API and UI

**Phase:** 12 — Admin
**Depends:** E17, E19
**Estimate:** 2–3 sessions
**Status:** Implemented (V1)

## Goal

Give operators one economy workspace: read the state, inspect the ledger, and perform the handful of actions that make scenarios possible.

## Context

The admin is a Blazor WebAssembly app (`Genelife.Web`) that polls `/api/data` every 5 s and renders tabs in `Data.razor`. Auth is handled by `GameDataService` using a loopback admin token. New economy state must fit this pattern; no direct world access from the browser.

## Design

### Endpoints

| Method | Route | Auth | Purpose |
|---|---|---|---|
| GET | `/api/economy/overview` | Player | accounts, totals, states |
| GET | `/api/economy/ledger` | Admin | filtered ledger |
| GET | `/api/economy/metrics/current` | Player | latest yearly snapshot |
| GET | `/api/economy/metrics/history` | Player | snapshot series |
| GET | `/api/economy/fiscal` | Admin | government and pension fund |
| POST | `/api/offices` | Admin | create office |
| PATCH | `/api/offices/{id}` | Admin | update capacity/cost |
| POST | `/api/job-postings` | Admin | open posting |
| PATCH | `/api/job-postings/{id}` | Admin | salary/seats/close |
| POST | `/api/companies/{id}/capital` | Admin | inject capital |
| POST | `/api/companies/{id}/freeze` | Admin | hiring freeze toggle |
| POST | `/api/employment/{simId}/fire` | Admin | separation |
| POST | `/api/companies/{id}/restructure` | Admin | force restructuring |
| GET | `/api/commands/{id}` | Admin | command result |

All mutations return `202` with `commandId`; the page shows pending/applied/rejected.

### Read model

Extend `WorldDataStatus` with:

```csharp
public EconomyStatus Economy { get; set; }
public List<CompanyAccountStatus> CompanyAccounts { get; set; }
public List<LedgerEntryStatus> RecentLedger { get; set; }
```

`EconomyStatus` carries money supply, wages, revenue, tax, arrears, unemployment, vacancy and health flags.

### UI

New `Pages/Economy.razor` with:

- summary cards: money supply, company cash, household cash, arrears, unemployment, vacancy;
- tabs: `Accounts`, `Wages`, `Postings`, `Offices`, `Ledger`, `Fiscal`, `Health`;
- inline actions with confirmation and pending state;
- filters: company, state, min amount, tick range;
- empty/error states consistent with `Data.razor`.

Keep `Data.razor` for raw entities; link each company row to the economy page filtered by that company.

## Tasks

- [ ] Add DTOs and endpoints; wire Wolverine attributes and admin policy.
- [ ] Extend `WorldDataStatus` and the `/api/data` payload.
- [ ] Add `GameDataService` methods for reads and commands.
- [ ] Build `Economy.razor` with polling, tabs and action dialogs.
- [ ] Add a ledger filter component with paging.
- [ ] Add command result feedback and optimistic refresh.
- [ ] Add navigation entry and deep links (`/economy?company=12`).
- [ ] Handle API-down and auth-expired states explicitly.

## Files

- new `Genelife.API/DTOs/EconomyStatus.cs`, `CompanyAccountStatus.cs`, `LedgerEntryStatus.cs`
- `Genelife.API/Endpoints/Wolverine/EconomyEndpoints.cs`
- `Genelife.API/DTOs/WorldDataStatus.cs`
- `Genelife.Web/Pages/Economy.razor`
- `Genelife.Web/Services/GameDataService.cs`
- `Genelife.Web/Layout/NavMenu.razor`

## Acceptance

- Admin can see current economy state without opening logs.
- Every mutation is available from the UI and reports its result.
- Ledger can be filtered by entity and kind and shows at least the last 1,000 entries.
- Page works when the simulation is stopped (shows an explanatory banner).
- Non-admin tokens receive 403 on mutations.
- No browser code mutates the world directly.

## Testing

- Endpoint authorization tests.
- DTO contract tests (camelCase, decimals).
- Blazor component render test for empty/populated/error states.
- Command flow test: action → pending → applied.

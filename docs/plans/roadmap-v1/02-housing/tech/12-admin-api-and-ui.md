# H12 — Admin API and UI

**Phase:** 9 — Admin
**Depends:** H10, H11, E17
**Estimate:** 2–3 sessions
**Status:** Planned

## Goal

Give operators a housing workspace: inspect stock, occupancy, rent roll and arrears, and perform the actions needed to unblock scenarios.

## Context

The admin is the same Blazor WebAssembly shell and polling model used by the economy page. Housing needs a distinct page because the entities and actions differ from raw sim tables.

## Design

### Endpoints

| Method | Route | Auth | Purpose |
|---|---|---|---|
| GET | `/api/housing/overview` | Player | stock, vacancy, rent index, homelessness |
| GET | `/api/housing/properties` | Player | property + unit list |
| GET | `/api/housing/households` | Player | households, members, tenure, arrears |
| GET | `/api/housing/rent-roll` | Admin | rent roll by landlord |
| POST | `/api/housing/properties` | Admin | create property/units |
| PATCH | `/api/housing/properties/{id}` | Admin | name, condition, upkeep |
| PATCH | `/api/housing/units/{id}` | Admin | rent, tier, capacity |
| POST | `/api/housing/assign` | Admin | assign household to unit |
| POST | `/api/housing/evict` | Admin | issue notice or evict now |
| POST | `/api/housing/construction` | Admin | start/cancel project |
| PATCH | `/api/housing/market` | Admin | rent index bounds |

All mutations enqueue an `AdminCommand` and return `202`.

### DTOs

`HousingStatus` contains:

- counts: properties, units, occupied, vacant, sheltered, homeless;
- money: rent roll, collected this month, arrears, upkeep;
- market: index, vacancy rate, pipeline units;
- alerts: overcrowded units, low condition, notices, shelter overflow.

### UI

`Pages/Housing.razor`:

- summary cards: occupancy, vacancy, rent index, arrears, homeless, shelter beds;
- tabs: `Properties`, `Units`, `Households`, `Rent roll`, `Arrears`, `Market`, `Construction`;
- actions with confirmation and command-result feedback;
- filters by district, tier, landlord, tenure and arrears status;
- deep link `/housing?property=4`.

## Tasks

- [ ] Add DTOs and endpoints with admin authorization.
- [ ] Extend `/api/data` and `WorldDataStatus` with housing status.
- [ ] Add `GameDataService` read and command methods.
- [ ] Build `Housing.razor` with tabs, filters and actions.
- [ ] Add a rent-roll table grouped by landlord.
- [ ] Add an arrears ageing view.
- [ ] Add construction pipeline and market index controls.
- [ ] Add navigation entry and cross-links from sim detail.

## Files

- new `Genelife.API/DTOs/HousingStatus.cs`
- `Genelife.API/Endpoints/Wolverine/HousingEndpoints.cs`
- `Genelife.API/DTOs/WorldDataStatus.cs`
- `Genelife.Web/Pages/Housing.razor`
- `Genelife.Web/Services/GameDataService.cs`
- `Genelife.Web/Layout/NavMenu.razor`

## Acceptance

- Admin can see occupancy and vacancy at a glance.
- Property/unit CRUD and assignment work through commands.
- Rent roll and arrears are accurate and filterable.
- Eviction and construction actions report results.
- Non-admin tokens get 403 on mutations.
- Page handles stopped simulation and API errors.

## Testing

- Authorization tests.
- DTO contract tests.
- Component render tests.
- Command flow test for assign/evict.
- Rent-roll aggregation test.

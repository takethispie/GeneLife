# L20 — Admin API and UI

**Phase:** 9 — Admin
**Depends:** L17, L19, E20, H12
**Estimate:** 3 sessions
**Status:** Planned

## Goal

Make the lifecycle observable and controllable: age pyramid, schools, careers, pensions, health, mortality and population inflow in one workspace.

## Context

The admin shell polls `/api/data` and renders pages per feature. Lifecycle data is the most cross-cutting: it links economy, housing and population. The page must expose both aggregate health and per-sim detail.

## Design

### Endpoints

| Method | Route | Auth | Purpose |
|---|---|---|---|
| GET | `/api/lifecycle/overview` | Player | stage counts, pyramid, health, pensions |
| GET | `/api/lifecycle/sims/{id}` | Player | stage, skills, career, health, pension |
| GET | `/api/lifecycle/schools` | Player | enrollment, capacity, quality |
| GET | `/api/lifecycle/pensions` | Admin | fund, statements, payouts |
| GET | `/api/lifecycle/mortality` | Player | log, causes, average age |
| GET | `/api/lifecycle/population` | Player | pyramid history, inflow status |
| POST | `/api/schools` | Admin | create school |
| PATCH | `/api/schools/{id}` | Admin | capacity, quality, emphasis |
| POST | `/api/sims/{id}/stage` | Admin | force transition |
| POST | `/api/sims/{id}/graduate` | Admin | force graduation |
| POST | `/api/sims/{id}/retire` | Admin | force retirement |
| POST | `/api/population/generate` | Admin | generate a world |
| POST | `/api/population/inflow` | Admin | trigger or pause inflow |
| POST | `/api/sims/{id}/care` | Admin | grant medical care |

Mutations enqueue `AdminCommand` and return `202`.

### DTOs

`LifecycleStatus`:

- stage counts and shares;
- age pyramid buckets (0–5, 6–17, 18–24, 25–34, 35–44, 45–54, 55–64, 65–79, 80+);
- enrollment, graduation, employment and unemployment by stage;
- pension fund, payouts, average replacement rate;
- health distribution, mortality by cause and average age at death;
- inflow: arrived, pending, paused, target.

`SimLifecycleStatus`:

- stage, age, stage age, school, employer, seniority, performance;
- health, decline rate, care status;
- pension balance and projected pension;
- household, housing tenure;
- flags: unemployed, homeless, in arrears, care needed.

### UI

`Pages/Lifecycle.razor`:

- summary cards: population, education share, work share, retirees, elders, average health;
- tabs: `Pyramid`, `Schools`, `Careers`, `Pensions`, `Mortality`, `Inflow`;
- a sim drill-down panel linking to economy and housing pages;
- actions with confirmation and command feedback;
- charts for pyramid history and mortality causes.

## Tasks

- [ ] Add DTOs and endpoints with authorization.
- [ ] Extend `/api/data` with lifecycle aggregates.
- [ ] Add `GameDataService` methods.
- [ ] Build `Lifecycle.razor` with tabs, charts and drill-down.
- [ ] Add school CRUD and enrollment actions.
- [ ] Add force-transition, graduation, retirement and care actions.
- [ ] Add population generation and inflow controls.
- [ ] Add cross-links to economy and housing.
- [ ] Handle stopped simulation, empty world and auth errors.

## Files

- new `Genelife.API/DTOs/LifecycleStatus.cs`, `SimLifecycleStatus.cs`
- `Genelife.API/Endpoints/Wolverine/LifecycleEndpoints.cs`, `PopulationEndpoints.cs`
- `Genelife.API/DTOs/WorldDataStatus.cs`
- `Genelife.Web/Pages/Lifecycle.razor`
- `Genelife.Web/Services/GameDataService.cs`
- `Genelife.Web/Layout/NavMenu.razor`

## Acceptance

- Admin can see the age pyramid and stage shares at a glance.
- School enrollment and capacity are visible and editable.
- Careers, health and pensions are inspectable per sim.
- Mortality log shows cause, age and estate status.
- Inflow can be triggered, paused and audited.
- Forced transitions are safe and rejected when invalid.
- Non-admin users cannot mutate lifecycle state.

## Testing

- Authorization tests.
- DTO contract tests.
- Page render tests for all tabs.
- Command flow tests for force transition and generation.
- Cross-feature link tests.

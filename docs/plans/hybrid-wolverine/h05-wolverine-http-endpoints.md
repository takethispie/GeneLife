# H05 — Wolverine HTTP endpoints

**Phase:** 1 — Foundation
**Depends:** H04
**Estimate:** 1 session

**Status:** Done

## Goal

Replace the existing ASP.NET minimal API endpoints for Sim and Company operations with Wolverine HTTP endpoints. Existing endpoints continue to work alongside Wolverine endpoints during migration. Wolverine endpoints are the new primary surface.

## Context

Wolverine can serve HTTP endpoints directly via `[WolverinePost]` / `[WolverineGet]` attributes on handler methods. This eliminates the separate `Endpoints/` extension classes and reduces boilerplate.

## Tasks

- [x] Create `Genelife.API/Endpoints/Wolverine/SimEndpoints.cs`:
  - `[WolverinePost("/api/sims")]` → maps `CreateSim` command → returns 202 with `NetworkId`
  - `[WolverineGet("/api/sims")]` → maps `GetAllSims` query → returns `List<SimStatus>`
  - `[WolverineGet("/api/sims/{networkId}")]` → maps `GetSim` query → returns `SimStatus?`
  - `[WolverinePost("/api/sims/{networkId}/action")]` → maps `SetSimAction` command → returns 202
  - `[WolverineDelete("/api/sims/{networkId}")]` → maps `DeleteSim` command → returns 202
- [x] Create `Genelife.API/Endpoints/Wolverine/CompanyEndpoints.cs`:
  - `[WolverinePost("/api/companies")]` → maps `CreateCompany` command
  - `[WolverineGet("/api/companies")]` → maps `GetAllCompanies` query
  - `[WolverineGet("/api/companies/{networkId}")]` → maps company lookup
  - `[WolverineDelete("/api/companies/{networkId}")]` → maps `DeleteCompany` command
- [x] Add `[assembly: WolverineModule]` attribute for endpoint discovery
- [x] Keep existing `SimulationEndpoints.cs` and `CompanyEndpoints.cs` but mark as `[Obsolete]`
- [x] Add HTTP-level integration test: `POST /api/sims` with valid body → 202 + NetworkId in response

## Files

- new `Genelife.API/Endpoints/Wolverine/SimEndpoints.cs`
- new `Genelife.API/Endpoints/Wolverine/CompanyEndpoints.cs`

## Conventions

- Wolverine endpoints return `(Response, T?)` tuples for status + body
- Commands return 202 Accepted (not 200 — the mutation hasn't happened yet)
- Queries return 200 with result
- Auth middleware is applied via `[Middleware(typeof(PlayerAuthMiddleware))]` on the handler class (not yet implemented, see H17)

## Acceptance

- `POST /api/sims` returns 202 with a valid NetworkId in the body
- `GET /api/sims` returns list (fallback to old implementation until H08)
- Old endpoints still work
- Wolverine endpoint tests pass

# H19 — Migrate remaining ASP.NET endpoints to Wolverine

**Phase:** 5 — Realtime and Cleanup
**Depends:** H05, H08, H09, H15
**Estimate:** 1 session

## Goal

Remove all legacy ASP.NET minimal API endpoints and their extension methods. All HTTP access goes through Wolverine endpoints exclusively.

## Tasks

- [ ] Add Wolverine endpoints for auth and simulation control:
  - `[WolverinePost("/api/auth/token")]` — JWT token generation
  - `[WolverinePost("/api/simulation/start")]` — admin-only
  - `[WolverinePost("/api/simulation/stop")]` — admin-only
- [ ] Remove `SimulationEndpoints.cs`, `CompanyEndpoints.cs`, `AuthEndpoints.cs`
- [ ] Remove `app.Map*Endpoints()` calls from `Program.cs`
- [ ] Verify all Wolverine endpoints are discovered and working
- [ ] Run existing integration tests; fix any HTTP routing differences

## Files

- new `Genelife.API/Endpoints/Wolverine/AuthEndpoints.cs`
- new `Genelife.API/Endpoints/Wolverine/SimulationControlEndpoints.cs`
- removed legacy endpoint files
- `Genelife.API/Program.cs`

## Acceptance

- All HTTP endpoints return correct status codes via Wolverine
- No legacy endpoint classes remain
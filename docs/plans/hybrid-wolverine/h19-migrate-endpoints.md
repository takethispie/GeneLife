# H19 — Migrate remaining ASP.NET endpoints to Wolverine

**Phase:** 5 — Realtime and Cleanup
**Depends:** H05, H08, H09, H15
**Estimate:** 1 session

**Status:** Done — auth, simulation control, data, map, player, admin and social routes all run through Wolverine HTTP; legacy files and mappings are removed.

## Goal

Remove all legacy ASP.NET minimal API endpoints and their extension methods. All HTTP access goes through Wolverine endpoints exclusively.

## Tasks

- [x] Add Wolverine endpoints for auth and simulation control:
  - `[WolverinePost("/api/auth/token")]` — JWT token generation
  - `[WolverinePost("/api/simulation/start")]` — admin-only
  - `[WolverinePost("/api/simulation/stop")]` — admin-only
- [x] Remove `SimulationEndpoints.cs`, `CompanyEndpoints.cs`, `AuthEndpoints.cs`
- [x] Remove `app.Map*Endpoints()` calls from `Program.cs`
- [x] Verify all Wolverine endpoints are discovered and working
- [x] Run existing integration tests; fix any HTTP routing differences

## Files

- new `Genelife.API/Endpoints/Wolverine/AuthEndpoints.cs`
- new `Genelife.API/Endpoints/Wolverine/SimulationControlEndpoints.cs`
- removed legacy endpoint files
- `Genelife.API/Program.cs`

## Acceptance

- All HTTP endpoints return correct status codes via Wolverine
- No legacy endpoint classes remain

# H02 — Register Wolverine in Program.cs

**Phase:** 1 — Foundation
**Depends:** H01
**Estimate:** 1 session

## Goal

Register Wolverine services in the ASP.NET host alongside the existing `SimulationEngine`, `SimulationLoop`, `SimulationManager` registrations. Wolverine must not interfere with existing services. Configure Wolverine for local (in-memory) transport in Phase 1.

## Context

Wolverine is a drop-in `IHostedService`. It discovers handlers by scanning assemblies. We configure it to use in-memory transport so no external broker (RabbitMQ, Azure Service Bus) is needed yet. Marten is configured with a local Postgres connection string.

## Tasks

- [ ] Add Postgres connection string to `appsettings.json` and `appsettings.Development.json`
- [ ] Add `builder.Services.AddMarten(...)` with document store configuration
- [ ] Add `builder.Host.UseWolverine(opts => ...)` with:
  - Assembly discovery for `Genelife.API` and `Genelife.Messages`
  - In-memory transport for local commands/events
  - Marten integration via `opts.UseMarten()`
- [ ] Configure Wolverine to NOT auto-publish events (explicit publish only in Phase 1)
- [ ] Verify `SimulationEngine`, `SimulationLoop`, `SimulationManager` still register and function
- [ ] Start the app: verify no DI resolution errors
- [ ] Check that Wolverine health check endpoint returns healthy

## Files

- `Genelife.API/Program.cs` — Marten + Wolverine registration added
- `Genelife.API/appsettings.json` — Postgres connection string

## Acceptance

- App starts without DI errors
- Existing simulation endpoints still work (GET /sims, POST /sims, GET /companies)
- Wolverine's internal health checks pass
- No Postgres required for simulation-only operations
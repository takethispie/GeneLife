# Hybrid Wolverine Migration Checklist

**Status:** All phases done. Feature flags and legacy endpoint files are removed; Wolverine HTTP, Marten and the command queue are unconditional.

## Prerequisites

- [ ] Postgres instance running (localhost:5432 or configured)
- [ ] `.config/dotnet-tools.json` has required CLI tools
- [ ] Run `dotnet restore` to ensure all packages are restored

## Step-by-step Migration

### Phase 1 — Foundation (done)

| Step | Flag | What to verify | Status |
|------|------|----------------|--------|
| H01 | — | Packages installed: WolverineFx, Marten, etc | Done |
| H02 | `UseWolverineEndpoints` | Wolverine registered in DI | Done |
| H03 | — | `Genelife.Messages` project compiles | Done |
| H04 | — | Command handlers exist in `Genelife.API/Handlers/` | Done |
| H05 | `UseWolverineEndpoints` | `GET /api/sims`, `POST /api/sims`, `POST /api/companies` all return correct status codes | Done |
| H06 | `UseMartenReads` | Tables `mt_doc_simdocument`, `mt_doc_companydocument` created | Done |

### Phase 2 — Read Migration (done)

| Step | Flag | What to verify | Status |
|------|------|----------------|--------|
| H07 | `UseMartenReads` | Simulation ticks publish `SimNeedsChanged` events, projections update documents | Done |
| H08 | `UseMartenReads` | `GET /api/sims` returns from Postgres (check logs: no "falling back to snapshot") | Done |
| H09 | `UseMartenReads` | `GET /api/companies` returns from Postgres | Done |
| H10 | — | `SnapshotsEnabled` flag controls snapshot build | Done |

### Phase 3 — Persistence (done)

| Step | Flag | What to verify | Status |
|------|------|----------------|--------|
| H11 | `UseMartenReads` | `WorldRestoreService` restores entities from Marten on startup | Done |
| H12 | — | Simulation saves to Marten documents | Done |
| H13 | `UseOutbox` | Outbox pattern with Marten + Wolverine | Done |
| H14 | — | All persistence tests pass | Done |

### Phase 4 — Commands and Sagas (done)

| Step | Flag | What to verify | Status |
|------|------|----------------|--------|
| H15 | `UseCommandQueue` | `EnqueuedCommand` drained during tick | Done |
| H16 | — | Hiring keeps the tested collect→decide→apply pipeline; partial saga scaffolding and unused hiring events were removed | Done |
| H17 | `UseWolverineEndpoints` | Ownership middleware rejects cross-player mutations | Done |

### Phase 5 — Realtime and Cleanup (done)

| Step | Flag | What to verify | Status |
|------|------|----------------|--------|
| H18 | `UseSignalR` | SignalR broadcasts via Wolverine side effects | Done |
| H19 | — | All HTTP goes through Wolverine endpoints; legacy files and `Map*Endpoints()` calls removed | Done |
| H20 | — | Flags removed; `appsettings.json` has no `FeatureFlags` section; `EndpointParityTests` guards routes/status/auth | Done |

## Verification Commands

```bash
# Build the single path
dotnet build GeneLife.sln

# Endpoint, command and persistence parity (requires Postgres)
docker compose up -d postgres
dotnet run --project Genelife.Api.IntegrationTests/Genelife.Api.IntegrationTests.csproj --no-launch-profile

# Full server suite
dotnet run --project Genelife.Domain.Tests/Genelife.Domain.Tests.csproj --no-launch-profile
```

## Rollback

There is no flag rollback. The previous build remains deployable and the pre-V5
snapshot is kept for data rollback; see the V5 release runbook.

## Cleanup (done)

Feature flag conditionals are removed from `Program.cs`.
`Genelife.API/Endpoints/SimulationEndpoints.cs`, `CompanyEndpoints.cs`, `AuthEndpoints.cs`, `WorldDataEndpoints.cs` and the hiring saga scaffolding are deleted.

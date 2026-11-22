# Hybrid Wolverine Migration Checklist

**Status:** Phases 1–3 done; Phase 4 partially done (H16); Phase 5 partially done (H19/H20 cleanup outstanding).

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

### Phase 4 — Commands and Sagas (H15/H17 done; H16 partial)

| Step | Flag | What to verify | Status |
|------|------|----------------|--------|
| H15 | `UseCommandQueue` | `EnqueuedCommand` drained during tick | Done |
| H16 | `UseSagas` | Hiring saga processes job posting lifecycle | Partial |
| H17 | `UseWolverineEndpoints` | Ownership middleware rejects cross-player mutations | Done |

### Phase 5 — Realtime and Cleanup (partial — H19/H20 cleanup outstanding)

| Step | Flag | What to verify | Status |
|------|------|----------------|--------|
| H18 | `UseSignalR` | SignalR broadcasts via Wolverine side effects | Done |
| H19 | — | All HTTP goes through Wolverine endpoints | Partial |
| H20 | — | Feature flags toggle architecture behavior | Partial |

## Verification Commands

```bash
# All flags OFF (pre-Wolverine behavior)
# Set all flags to false in appsettings.json, then:
dotnet test Genelife.UnitTests --filter "FullyQualifiedName~AuthorizationTests"

# Wolverine endpoints ON
# Set UseWolverineEndpoints=true, others false:
dotnet test Genelife.UnitTests --filter "FullyQualifiedName~WolverineEndpointsTests"

# Marten reads ON
# Set UseWolverineEndpoints=true, UseMartenReads=true:
# Requires Postgres
dotnet test Genelife.UnitTests --filter "FullyQualifiedName~DocumentStoreTests"

# Full architecture (all flags ON)
# Requires Postgres
dotnet test Genelife.UnitTests
```

## Rollback

If any flag causes issues, disable it in `appsettings.json` and redeploy:

```json
{
  "FeatureFlags": {
    "UseWolverineEndpoints": false,
    "UseMartenReads": false,
    "UseCommandQueue": false,
    "UseSagas": false,
    "UseSignalR": true,
    "UseOutbox": false
  }
}
```

## Cleanup (after verification)

Remove feature flag conditionals from `Program.cs`.
Delete `Genelife.API/Endpoints/SimulationEndpoints.cs` and `CompanyEndpoints.cs`.
Remove old `AuthEndpoints.cs`.

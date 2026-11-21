# Hybrid Wolverine Migration Checklist

## Prerequisites

- [ ] Postgres instance running (localhost:5432 or configured)
- [ ] `.config/dotnet-tools.json` has required CLI tools
- [ ] Run `dotnet restore` to ensure all packages are restored

## Step-by-step Migration

### Phase 1 — Foundation (already implemented)
| Step | Flag | What to verify |
|------|------|----------------|
| H01 | — | Packages installed: WolverineFx, Marten, etc |
| H02 | `UseWolverineEndpoints` | Wolverine registered in DI |
| H03 | — | `Genelife.Messages` project compiles |
| H04 | — | Command handlers exist in `Genelife.API/Handlers/` |
| H05 | `UseWolverineEndpoints` | `GET /api/sims`, `POST /api/sims`, `POST /api/companies` all return correct status codes |
| H06 | `UseMartenReads` | Tables `mt_doc_simdocument`, `mt_doc_companydocument` created |

### Phase 2 — Read Migration
| Step | Flag | What to verify |
|------|------|----------------|
| H07 | `UseMartenReads` | Simulation ticks publish `SimNeedsChanged` events, projections update documents |
| H08 | `UseMartenReads` | `GET /api/sims` returns from Postgres (check logs: no "falling back to snapshot") |
| H09 | `UseMartenReads` | `GET /api/companies` returns from Postgres |
| H10 | — | `SnapshotsEnabled` flag controls snapshot build |

### Phase 3 — Persistence (requires Postgres)
| Step | Flag | What to verify |
|------|------|----------------|
| H11 | `UseMartenReads` | `WorldRestoreService` restores entities from Marten on startup |
| H12 | — | Simulation saves to Marten documents |
| H13 | `UseOutbox` | Outbox pattern with Marten + Wolverine |
| H14 | — | All persistence tests pass |

### Phase 4 — Commands and Sagas (requires Postgres for H16)
| Step | Flag | What to verify |
|------|------|----------------|
| H15 | `UseCommandQueue` | `EnqueuedCommand` drained during tick |
| H16 | `UseSagas` | Hiring saga processes job posting lifecycle |
| H17 | `UseWolverineEndpoints` | Ownership middleware rejects cross-player mutations |

### Phase 5 — Realtime and Cleanup
| Step | Flag | What to verify |
|------|------|----------------|
| H18 | `UseSignalR` | SignalR broadcasts via Wolverine side effects |
| H19 | — | All HTTP goes through Wolverine endpoints |
| H20 | — | Feature flags toggle architecture behavior |

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
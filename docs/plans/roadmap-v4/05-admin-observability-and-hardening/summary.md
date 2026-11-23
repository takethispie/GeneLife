# Admin, Observability and Hardening — Summary

**Folder:** `05-admin-observability-and-hardening/`
**Status:** Done (V4a) — see [release checklist](release-checklist.md)

**Depends on:** features 01–04
**Feeds:** release, operations, long-run tuning

## Objective

Make the family system operable, auditable, deterministic and durable. A feature that cannot be inspected, replayed or migrated is not ready for a live world, even if the unit tests pass.

## Baseline before V4

- Admin pages exist for Data, Economy, Housing, Lifecycle and Map.
- The lifecycle page exposes population, schools, pensions, mortality and immigration.
- Events are written asynchronously by `SimulationEventWriter`; projections exist for sims, companies and maps.
- `BalanceScenarioTests` covers a 10-year economy/housing/population run.
- `PersistedWorldState` schema 5 persists options and the `BalanceBackfilled` migration.

## Deliverables

1. Family read model and DTOs for sims, traits, relationships, pregnancies, children, guardians and health.
2. Family admin page and controlled operations.
3. Complete family event family and Marten projections.
4. Determinism contract and replay suite.
5. Performance and scale guardrails at 2,000 sims.
6. Final schema 5 → 10 migration, restore and double-restore coverage.
7. Scenario suite and acceptance report.
8. Release checklist and rollout notes.

## Architecture

```
World / systems
   │
   ├── events ──► SimulationEventWriter ──► Marten projections
   │                                            │
   └── live read model ─────────────────────────┤
                                                ▼
                                         Family admin page
                                                │
                                       typed admin commands
                                                │
                                                ▼
                                         tick-loop apply
```

The live read model is authoritative for the current tick; projections are the durable history. The admin can show a stale-projection warning and fall back to live state.

## Tech steps

| # | Step | Phase |
|---|---|---|
| 01 | [Family read model and DTOs](tech/01-family-read-model-and-dtos.md) | Admin |
| 02 | [Admin page and operations](tech/02-admin-page-and-operations.md) | Admin |
| 03 | [Events and Marten projections](tech/03-events-and-marten-projections.md) | Events |
| 04 | [Determinism and replay](tech/04-determinism-and-replay.md) | Verification |
| 05 | [Performance and scale](tech/05-performance-and-scale.md) | Verification |
| 06 | [Persistence, restore and migration](tech/06-persistence-restore-and-migration.md) | Durability |
| 07 | [Scenario and acceptance tests](tech/07-scenario-and-acceptance-tests.md) | Verification |
| 08 | [Release checklist](tech/08-release-checklist.md) | Release |

## Business reading

- [Operability and trust](business/01-operability-and-trust.md)
- [Release and learning](business/02-release-and-learning.md)

## Definition of done

- The family page shows traits, relationships, partnerships, pregnancies, children, guardians and health checks.
- Admin operations are typed, authorized, audited and visible after the next tick.
- Every family event has an idempotent projection and a rebuild path.
- The same seed and options reproduce the same family graph, births and money.
- The 2,000-sim decade stays inside the tick budget.
- Schema 5 → 10 migration, restore and double restore are exact.
- The scenario suite and the existing balance suite pass.
- The release checklist is complete and the rollout notes are recorded.
# Roadmap V1 — Status

**Status:** Implemented (V1)
**Release:** V1 on `master`
**Baseline:** economy, housing and life stages

## Completed

| Area | Status | Evidence |
|---|---|---|
| Economy | Implemented | Company accounts, ledger, payroll, arrears, revenue, tax, pension contributions, credit, insolvency, savings, admin API/UI |
| Housing | Implemented | Households, registry, rent, utilities, arrears/eviction, allocation, quality effects, construction, ownership, admin API/UI |
| Life stages | Implemented | Aging, stage transitions, schools/skills, work/attendance/careers, unemployment, retirement/pensions, health/mortality, estates, immigration, generator, metrics, admin API/UI |

All tech-step documents under `01-economy/`, `02-housing/` and `03-life-stages/` are marked `Implemented (V1)`.

## Verification

- `dotnet build GeneLife.sln` succeeds.
- Full test suite: 329 passed, 2 skipped (manual 10k load reports), 0 failed.
- Integration tests require PostgreSQL; `docker compose up -d postgres` provides the local database.
- Each test fixture uses an isolated Marten schema and snapshot file. The isolated schema is dropped on fixture disposal.
- Performance guardrails cover 1k, 2k and 5k synthetic NPC tiers; the 1k baseline is calibrated to the current measured allocation and p95.
- Restore coverage includes legacy v3→v4 snapshots, pending estates, owned-home transfer, household head reassignment and a 10-year population scenario.

## Known limitations

- V1 is a backend/admin simulation. There is no player-facing game client in this roadmap.
- Population balance is validated for stability, not tuned to a specific target unemployment or wealth distribution.
- Performance guardrails are synthetic-tier budgets, not an end-to-end browser load test.
- Marten read-model projections are eventually consistent through the background event writer.
- The full suite requires PostgreSQL; unit-only runs still exercise systems that do not need Marten.

## Next

1. Run long-run balance experiments at 2k+ population and tune inflow, wages, rent and pension parameters.
2. Extend the lifecycle page with any remaining operator reports needed for live operations.
3. Start a V2 client/gameplay roadmap if the backend scope is complete.

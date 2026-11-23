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
- Population balance is asserted at 200 sims for a decade (`BalanceScenarioTests`); 1k/2k long-run tuning is still open.
- The 2026-11 balance pass fixed the fixed-company seed, money leaks and pension capitalization; vacancy, company failures and wealth Gini remain outside target (see `01-economy/business/04-balance-targets.md`).
- Performance guardrails are synthetic-tier budgets, not an end-to-end browser load test.
- Marten read-model projections are eventually consistent through the background event writer.
- The full suite requires PostgreSQL; unit-only runs still exercise systems that do not need Marten.

## Next

1. Run the balance scenario at 1k–2k population and close the remaining gaps: turnover for vacancy/failures, wealth recycling for Gini, and demand recycling for wage share.
2. Extend the lifecycle page with any remaining operator reports needed for live operations.
3. V2, V3 and the 2026-11 balance pass are complete; the next roadmap is [V4 relationships and reproduction](../roadmap-v4/README.md).

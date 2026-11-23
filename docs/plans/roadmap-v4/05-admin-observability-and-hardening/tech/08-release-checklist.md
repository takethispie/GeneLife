# H08 — Release checklist

**Phase:** 8 — Release
**Depends:** H01–H07
**Estimate:** 1 session
**Status:** Planned (V4)

## Goal

Ship V4 without silently changing existing worlds, losing family facts or regressing the balance loop.

## Checklist

### Build and tests

- [ ] `dotnet build GeneLife.sln` succeeds.
- [ ] Full unit/integration suite passes with PostgreSQL.
- [ ] Family fast suites pass.
- [ ] Family decade, generation and scale scenarios pass.
- [ ] Replay suite passes.
- [ ] Migration and double-restore tests pass.
- [ ] Conservation and trust gaps are zero.
- [ ] The 10-year balance scenario is green.

### Data and migration

- [ ] Schema 5 → 10 migration rehearsed on a real save copy.
- [ ] No invented parent edges for legacy households.
- [ ] Traits are deterministic across double restore.
- [ ] Family options/coefficients are persisted.
- [ ] `BalanceBackfilled` and the 2026-11 backfill remain intact.
- [ ] Snapshot size is within budget.
- [ ] Rollback build is available.

### Admin

- [ ] Family page loads with empty, seeded and migrated worlds.
- [ ] All operations are admin-only and audited.
- [ ] Health checks and stale-projection indicators work.
- [ ] No player route exposes traits, partner search or a ranking.
- [ ] `EmptyState` and command-result patterns are consistent.

### Performance

- [ ] 2,000-sim decade inside tick budget.
- [ ] Candidate p95/max within cap.
- [ ] No full population pair scan.
- [ ] Projection lag within the acceptable window.
- [ ] Snapshot and payload sizes recorded.

### Balance

- [ ] Births are a visible continuity path.
- [ ] Replacement rate and age pyramid are documented.
- [ ] Child costs and arrears are visible.
- [ ] Orphans have guardians or state care.
- [ ] Inheritance and trusts conserve money.
- [ ] Known target misses are documented (wealth Gini, vacancy, company failures).

### Documentation

- [ ] `STATUS.md` updated with evidence and known gaps.
- [ ] Balance-target doc updated with the family report.
- [ ] Admin page documented.
- [ ] Migration and rollback notes documented.
- [ ] Open questions either resolved or explicitly deferred.

### Release

- [ ] Release job passes.
- [ ] Staged deployment on a copy of production data.
- [ ] Admin smoke test after deploy.
- [ ] 10-year run on the deployed copy.
- [ ] Monitoring for invariants, projection lag and tick budget.
- [ ] Sign-off recorded with commit id and date.

## Rollback

- Keep the previous build and the pre-migration snapshot copy.
- A rollback restores the previous build and the saved V5 state.
- Do not attempt to downgrade a V6 save in place.
- Record the rollback decision and the reason in `STATUS.md`.

## Known gaps to carry forward

- Wealth Gini remains above the V1 target.
- Vacancy and company-failure bands remain unmet.
- Divorce, adoption and a richer genome are deferred.
- Player-facing family UI is not in V4a.

## Acceptance

- Every checklist item is checked or has a documented exception.
- The release report and rollback plan are reviewed.
- No silent default change affects an existing save.
- No family invariant is knowingly failing at release.
# Feature 05 — Release Checklist

**Feature:** Admin, observability and hardening
**Status:** Implemented and hardened (V4a)
**Date:** 2026-11-23

## 1. Build and unit gate

| Check | Command | Result |
|---|---|---|
| Solution build | `dotnet build GeneLife.sln` | Pass |
| Family suite | `dotnet run --project Genelife.UnitTests/Genelife.UnitTests.csproj --no-launch-profile -- -class "Genelife.UnitTests.Family.*"` | Pass |
| Persistence suite | `... -class "Genelife.UnitTests.Persistence.*"` | Pass |
| Performance suite | `... -class "Genelife.UnitTests.Performance.FamilyPerformanceTests"` | Pass (5/5) |
| Projection coverage | `... -method "Genelife.UnitTests.Marten.FamilyProjectionCoverageTests.EveryDurableFamilyEventHasAProjectionHandler"` | Pass |
| Full PostgreSQL integration | `docker compose up -d postgres && dotnet run --project Genelife.UnitTests/Genelife.UnitTests.csproj --no-launch-profile` | Pass (531 total, 0 failed, 2 skipped, ~73 s) |

## 2. Admin smoke test

1. Start the API and web app with an admin token.
2. Open `/family`.
3. Confirm the overview shows population, partnerships, births, orphans, state care, trait distribution and health checks.
4. Filter sims by stage, partnered, has children, household and name; open a sim and verify traits, relationships, household, children and pregnancies.
5. Force and end a partnership with a reason; confirm the change appears after the next tick and the reason is present in `FamilyAdminActionApplied`.
6. Force and cancel a pregnancy; confirm status and audit.
7. Assign a guardian to a state-care child; confirm state care clears and a Guardian edge exists.
8. Release a trust with a reason; confirm the beneficiary is paid once and the trust is gone.
9. Confirm every destructive button requires the confirmation checkbox.
10. Confirm `/api/family/*` returns 401/403 without the admin policy.

Browser E2E for the page shell, tabs, confirmations and command results:

```bash
cd clients/browser
npm ci
npx playwright install chromium
npm run test:e2e:admin
```

## 3. Performance evidence

| Scenario | Result |
|---|---|
| 200 sims, 10 years, candidate cap and invariants | Pass |
| 2,000 sims, 1 year, tick p95 ≤ 15 ms and candidate max ≤ cap | Pass |
| 5,000 sims, 200 ticks smoke | Pass |
| 2,000 sims, 10 years | Pass — ending population 1,839, tick p95 3.1202 ms, p99 57.6272 ms, 26.1 s, restore 1,839/1,839, 278 births / 602 deaths, 0 orphans, 63 state-care, 0 health Fail, candidate max 5/24; see [release evidence](release-evidence.md) |
| 30 years, 200 sims continuity | Pass — population 189, 62 births / 118 deaths, 0 orphans, 3 state-care, money 1,900,469,300 cents, 0 continuity/reproduction Fail (4 reproduction Warn); see [release evidence](release-evidence.md) |
| Restore then 1,000 ticks at 200 sims | Pass |

Raw scenario JSON lives in `Genelife.UnitTests/bin/<Configuration>/net10.0/release-reports/`. `FamilyStatus.Performance` exposes last/p95/p99 tick time, allocation per tick, candidate average/p95/max/cap and the worst system. `Metrics` continues to log per-system timings.

## 4. Migration runbook (schema 10 → 11)

Schema 10 adds `PersistedWorldState.StateCare` and `PersistedRelationship.EndedTick`; schema 11 adds `PersistedSim.HasWallet`.

1. Stop the simulation and allow the current snapshot to finish.
2. Back up the SQLite snapshot file and the PostgreSQL Marten schema.
3. Deploy the new build.
4. Restore the snapshot. `SnapshotMigrations.Upgrade` adds an empty state-care list for schema ≤ 9 and backfills `HasWallet` for schema ≤ 10 by deriving it from a non-zero `WalletBalance`/`WalletBalanceCents` or a non-zero `EmployerNetworkId`.
5. Confirm the engine starts, `FamilyStatus.Health` has no `Fail`, `BuildGuardianships` shows state-care rows and wallet-bearing sims restore with their balances and employers intact.
6. Run the family and persistence suites against the migrated save.

**Rollback:** schemas 10 and 11 are additive. To roll back, stop the app, restore the pre-upgrade snapshot backup and redeploy the previous build. Do not edit `CurrentSchemaVersion` manually.

**Failure policy:** restore prunes legacy relationship edges whose endpoint is missing from the snapshot. Active/ready pregnancies with a missing parent, unknown relationship kinds/sources, unknown pregnancy status/sources, and trusts with a missing beneficiary still fail loudly. Resolved pregnancy history whose parent was destroyed is dropped instead of making the save unrestorable.

## 5. Observability

- `GET /api/family` — read model, projection source/staleness, performance, health.
- `GET /api/family/health` — invariant and continuity checks.
- `GET /api/family/sims`, `/relationships`, `/pregnancies`, `/children`, `/guardianships`, `/traits`, `/households`, `/attraction`.
- Admin mutations: `/api/family/partnerships/force`, `/partnerships/end`, `/pregnancies`, `/pregnancies/{id}/cancel`, `/guardianships`, `/trusts/{id}/release`.
- Marten projections: `FamilyDocument`, `RelationshipDocument`, `PregnancyDocument`, `ChildDocument`, `HouseholdCompositionDocument`, `EstateDocument`.
- `PairingRejected` is emitted for the first 100 rejections and then sampled one in fifty to bound event-store growth.

## 6. Known gaps

- The 2,000-sim ten-year run and the 30-year continuity scenario are recorded and green; see [release evidence](release-evidence.md). The 61 health warnings in the 2,000-sim ten-year run and the four reproduction `Warn` levels in the 30-year run are documented signals, not `Fail`s.
- The admin page has HTTP-level smoke coverage and a Playwright browser suite for tabs, sim detail, empty states, confirmation gating and command success/failure results. The browser suite mocks the family API; live apply, audit and authorization remain covered by the HTTP smoke test and PostgreSQL integration tests.
- `CohabitOnPairing` remains false. Enabling it breaks strict balance (wage share 0.9078 → 1.0527, vacancy 0.0854 → 0.2152) and worsens the 30-year run (overcrowded 1 → 12, vacancy 20.12% → 31.65%, health warnings 4 → 27). It needs economy/consumption recalibration and seeded-pair cohabitation, not a housing-only tweak.
- Household composition projections are approximate; the live read model is authoritative for member-level data.
- Same-sex pairing and adoption/surrogacy remain deferred.
- Trait visibility is admin-only; no public ranking exists.

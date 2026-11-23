# Feature 05 — Release Checklist

**Feature:** Admin, observability and hardening
**Status:** Implemented (V4a)
**Date:** 2026-11-23

## 1. Build and unit gate

| Check | Command | Result |
|---|---|---|
| Solution build | `dotnet build GeneLife.sln` | Pass |
| Family suite | `dotnet run --project Genelife.UnitTests/Genelife.UnitTests.csproj --no-launch-profile -- -class "Genelife.UnitTests.Family.*"` | Pass |
| Persistence suite | `... -class "Genelife.UnitTests.Persistence.*"` | Pass |
| Performance suite | `... -class "Genelife.UnitTests.Performance.FamilyPerformanceTests"` | Pass (5/5) |
| Projection coverage | `... -method "Genelife.UnitTests.Marten.FamilyProjectionCoverageTests.EveryDurableFamilyEventHasAProjectionHandler"` | Pass |
| Full PostgreSQL integration | `docker compose up -d postgres && dotnet run --project Genelife.UnitTests/Genelife.UnitTests.csproj --no-launch-profile` | Required before release; Marten tests need PostgreSQL |

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

## 3. Performance evidence

| Scenario | Result |
|---|---|
| 200 sims, 10 years, candidate cap and invariants | Pass |
| 2,000 sims, 1 year, tick p95 ≤ 15 ms and candidate max ≤ cap | Pass |
| 5,000 sims, 200 ticks smoke | Pass |
| 2,000 sims, 10 years | Not yet run; required before release |
| Restore then 1,000 ticks at 200 sims | Pass |

`FamilyStatus.Performance` exposes last/p95/p99 tick time, allocation per tick, candidate average/p95/max/cap and the worst system. `Metrics` continues to log per-system timings.

## 4. Migration runbook (schema 9 → 10)

Schema 10 adds `PersistedWorldState.StateCare` and `PersistedRelationship.EndedTick`.

1. Stop the simulation and allow the current snapshot to finish.
2. Back up the SQLite snapshot file and the PostgreSQL Marten schema.
3. Deploy the new build.
4. Restore the snapshot. `SnapshotMigrations.Upgrade` adds an empty state-care list for schema ≤ 9.
5. Confirm the engine starts, `FamilyStatus.Health` has no `Fail`, and `BuildGuardianships` shows state-care rows.
6. Run the family and persistence suites against the migrated save.

**Rollback:** schema 10 is additive. To roll back, stop the app, restore the pre-upgrade snapshot backup and redeploy the previous build. Do not edit `CurrentSchemaVersion` manually.

**Failure policy:** restore prunes legacy relationship edges whose endpoint is missing from the snapshot. Active/ready pregnancies with a missing parent, unknown relationship kinds/sources, unknown pregnancy status/sources, and trusts with a missing beneficiary still fail loudly. Resolved pregnancy history whose parent was destroyed is dropped instead of making the save unrestorable.

## 5. Observability

- `GET /api/family` — read model, projection source/staleness, performance, health.
- `GET /api/family/health` — invariant and continuity checks.
- `GET /api/family/sims`, `/relationships`, `/pregnancies`, `/children`, `/guardianships`, `/traits`, `/households`, `/attraction`.
- Admin mutations: `/api/family/partnerships/force`, `/partnerships/end`, `/pregnancies`, `/pregnancies/{id}/cancel`, `/guardianships`, `/trusts/{id}/release`.
- Marten projections: `FamilyDocument`, `RelationshipDocument`, `PregnancyDocument`, `ChildDocument`, `HouseholdCompositionDocument`, `EstateDocument`.
- `PairingRejected` is emitted for the first 100 rejections and then sampled one in fifty to bound event-store growth.

## 6. Known gaps

- The 2,000-sim ten-year run and the 30-year continuity scenario are not yet recorded; only the 200-sim decade and 2,000-sim one-year budget are green.
- Marten integration tests require PostgreSQL and were not part of the local no-database run.
- Household composition projections are approximate; the live read model is authoritative for member-level data.
- `CohabitOnPairing` remains false pending housing recalibration.
- Same-sex pairing and adoption/surrogacy remain deferred.
- Trait visibility is admin-only; no public ranking exists.

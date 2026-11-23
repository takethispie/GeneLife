# Feature 05 — Release Evidence

**Feature:** Admin, observability and hardening
**Date:** 2026-11-23
**Schema:** 11 (`PersistedWorldState.CurrentSchemaVersion = 11`)
**Verdict:** V4 release-ready except `CohabitOnPairing`, which stays disabled pending economy/consumption recalibration.

This file records the hardening run that closed the feature 05 release. It is the source of truth behind the numbers quoted in [STATUS.md](../../STATUS.md) and the [release checklist](release-checklist.md).

## 1. Commands run

| Purpose | Command |
|---|---|
| Release scenarios | `dotnet run --project Genelife.UnitTests/Genelife.UnitTests.csproj --no-build --no-launch-profile -- -class "Genelife.UnitTests.Release.FamilyReleaseScenarioTests" -noColor` |
| Full suite with PostgreSQL | `docker compose up -d postgres && dotnet run --project Genelife.UnitTests/Genelife.UnitTests.csproj --no-launch-profile` |
| Admin HTTP smoke | live API + web app with an admin token; `curl` against `/api/family/*` and `/family` |

Both release scenarios run with seed `21`. Raw reports are written to `Genelife.UnitTests/bin/<Configuration>/net10.0/release-reports/`:

- `two-thousand-sims-10-years.json`
- `two-thousand-sims-10-years-progress.json` (per-year progress)
- `thirty-year-continuity.json`

## 2. Full-suite result (PostgreSQL)

| Metric | Result |
|---|---|
| Total | 531 |
| Failed | 0 |
| Skipped | 2 |
| Duration | ~73 s |

Marten/integration namespaces run against PostgreSQL; the two skips are the pre-existing platform-gated tests.

## 3. 2,000 sims / 10 years

Source: `release-reports/two-thousand-sims-10-years.json`.

| Metric | Value |
|---|---|
| Seed | 21 |
| Ending population | 1,839 |
| Restored population | 1,839 (`restoreSucceeded = true`) |
| Tick p95 | 3.1202 ms |
| Tick p99 | 57.6272 ms |
| Elapsed | 26,104.8 ms |
| Births recorded | 278 |
| Deaths recorded | 602 |
| Orphans | 0 |
| State-care children | 63 |
| Money supply | 15,762,137,200 cents |
| Health failures | 0 |
| Health warnings | 61 |
| Candidate max / cap | 5 / 24 |

No `Fail` from `FamilyStatus.Health`. p95 stays well inside the ≤ 15 ms budget; the higher p99 is the monthly family batch.

## 4. 30 years / 200 sims

Source: `release-reports/thirty-year-continuity.json`.

| Metric | Value |
|---|---|
| Seed | 21 |
| Ending population | 189 |
| Births recorded | 62 |
| Deaths recorded | 118 |
| Orphans | 0 |
| State-care children | 3 |
| Money supply | 1,900,469,300 cents |
| Continuity failures | 0 |
| Reproduction failures | 0 |
| Reproduction levels | `Warn: 4` |
| Elapsed | 3,932.4 ms |

Continuity and reproduction report no `Fail` over three decades; the four `Warn` levels are the documented slow-growth signal, not a blocker.

## 5. Admin smoke test

| Check | Result |
|---|---|
| Family reads (`/api/family`, `/sims`, `/relationships`, `/pregnancies`, `/children`, `/guardianships`, `/traits`, `/households`, `/attraction`) | 200 |
| Unauthorized read | 401 |
| Force partnership | Applied and audited |
| End partnership | Applied and audited |
| Web `/family` | 200, Blazor bootstrap present |

**Limitation:** this is HTTP-level smoke only. No browser automation was available, so interactive page behaviour (tab refresh, confirmation checkbox, command-result rendering) was not exercised by an automated test. Feature 05 documented this gap; browser E2E automation is tracked separately.

## 6. Cohabitation (`CohabitOnPairing`)

`CohabitOnPairing` remains **false**. Enabling it fails the strict balance bands:

| Metric | Off (current) | On (attempt) | Band |
|---|---|---|---|
| Wage share | 0.9078 | 1.0527 | 0..1.0 |
| Vacancy | 0.0854 | 0.2152 | 0..0.12 |
| Overcrowded households (30-year run) | 1 | 12 | — |
| Vacancy (30-year run) | 20.12% | 31.65% | — |
| Health warnings (30-year run) | 4 | 27 | — |

Merging partners on pairing pulls extra residents into units, raising crowding and vacancy pressure and pushing the wage share past its ceiling. This needs economy/consumption recalibration plus seeded-pair cohabitation, not a housing-only tweak. The pairing policy stays opt-in until that recalibration lands.

## 7. Why this closes feature 05

- Full suite green with PostgreSQL.
- Both release scenarios recorded at target scale (2,000 sims / 10 years and 30 years).
- Restore verified at 2,000 sims.
- Admin family surface reachable and authorized at HTTP level.
- The only open balance item, cohabitation, is explicitly deferred with before/after evidence rather than silently enabled.

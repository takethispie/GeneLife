# Feature 05 — Release Evidence

**Status:** Implementation complete; release sign-off pending the 30-minute live soak and a rehearsal against a pre-V5 backup
**Verdict:** Conditional go for the seeded preview; not yet a production sign-off
**Date:** 2026-11-24
**Commit:** working tree (commit after review)
**Hardware:** Apple M3, 16 GB RAM, macOS; Playwright 1.65.1; .NET 10.0

Each section points to a test, scenario report or smoke run. Items that were not
exercised are exceptions, not checkmarks.

## 1. Build and tests

| Check | Result | Evidence |
|---|---|---|
| `dotnet build GeneLife.sln` | Pass | final build, 0 errors |
| Full PostgreSQL suite | Pass | `Genelife.Api.IntegrationTests`: 69 tests, 0 failed |
| Player suite | Pass | 560-test domain run, 0 failed |
| Appearance suite | Pass | included; `AppearanceDomainTests` + composition budget |
| Consumption suite | Pass | included; conservation/tariff/grocery tests |
| Social suite | Pass | included; social suite + 32-player soak |
| Persistence suite | Pass | included; schema/restore/double-restore tests |
| Performance suite | Pass | 33 tests (32 + appearance budget), 0 failed, 2 skipped (manual 10k load); cohabitation comparison passed separately |
| Client typecheck/lint/test/build | Pass | typecheck, eslint, 178 tests, `vite build` |
| Client E2E and live suite | Pass | fixture 5, mobile 3 (2 fixture + 1 live), live 5; admin 6 mocked + 1 live |

Commands:

```bash
dotnet run --project Genelife.Domain.Tests/Genelife.Domain.Tests.csproj --no-launch-profile -- -trait- "Category=Scenario"
dotnet run --project Genelife.Api.IntegrationTests/Genelife.Api.IntegrationTests.csproj --no-launch-profile
dotnet run --project Genelife.Performance.Tests/Genelife.Performance.Tests.csproj --no-launch-profile
cd clients/browser && npm run typecheck && npm run lint && npm run test && npm run build
E2E_LIVE=1 E2E_API_BASE_URL=http://localhost:5288 npx playwright test e2e/live-acceptance.spec.ts --project=chromium
E2E_ADMIN_LIVE=1 npx playwright test --config playwright.admin.config.ts
```

## 2. Player life

- Job → home → consumption → family → death → continuation: automated suite and the
  seeded live smoke; manual narrative walkthrough still required at sign-off.
- Replay with recorded intent log: covered by the player-life replay tests.
- 32-player soak: Pass, 32 players/1,400 ticks, p95 0.88 ms, no message loss or
  duplicate edges.
- Server authority and audit: every action is a typed intent drained on the tick;
  `EndpointParityTests` and the player action audit tests are green.
- Occupied-building relocation: 7 `BuildingRelocationTests` pass, covering
  residents, employees, students, housing paths, service links, map version and
  lot-reservation release. An in-lot move releases the building's own
  reservation and returns `ReservedTileCount` to zero.

## 3. Appearance

- Determinism and inheritance: Pass (`AppearanceDomainTests`, inheritance tests).
- Family resemblance and aging: Pass; admin `/appearance` live smoke loaded.
- No per-sim textures: Pass; shared layer contract, no texture allocation.
- Payload and frame budget: 2,000 descriptors composed in 1.86 ms (one frame).
- Admin preview: Pass (live admin smoke).

## 4. Consumption

- Utility networks, load and state: Pass.
- Metered billing, arrears and outages: Pass.
- Grocery stock, prices and spoilage: Pass.
- Nutrition effects: Pass.
- Conservation: Pass.
- Admin page: Pass (live admin smoke).

## 5. Social

- Presence scopes and privacy: Pass.
- Chat, emotes and retention: Pass.
- Friends, blocks, mutes and reports: Pass.
- Player partnerships and cohabitation: Pass.
- Venues and activities: Pass.
- Moderation queue and audit: Pass; admin `/social` live smoke.
- 32-player soak: Pass (see §7).

## 6. Balance and cohabitation

10-year reports with seed 21, municipality utility subsidy at default 100%:

| Metric | V4 baseline | 1,000 sims | 2,000 sims | Target | Status |
|---|---:|---:|---:|---:|---|
| Wage share | 0.9078 | 0.7417 | 0.7940 | Report | Inside 0–1 |
| Job vacancy | 0.0854 | 0.0339 | 0.0283 | Report | Reported |
| Company failures | — | 0 | 0 | Report | Zero |
| Wealth Gini | — | 0.5927 | 0.5755 | ≤ 0.70 | Pass |
| Unemployment | — | 0.0750 | 0.0913 | ≤ 0.10 | Pass |
| Median rent burden | — | 0.4284 | 0.3464 | ≤ 0.40 | **Miss at 1k** |
| Housing vacancy | — | 0.0844 | 0.0743 | ≤ 0.12 | Pass |
| Homeless households | 0 | 0 | 0 | 0 | Pass |
| End living | — | 982 | 1,937 | 80–120% | Pass |
| Money growth / decade | — | 8.27% | 8.20% | 0–10% | Pass |
| Conservation gap | — | 0 | 0 | 0 | Pass |
| Overcrowded units | 1 | 12 | 19 | Report | Reported |
| Economy/housing health warnings | 4 | 2 | 1 | Report | Reported |
| Births / deaths | — | 18 / 38 | 31 / 35 | Report | Reported |

### Cohabitation comparison

200 sims, seed 21, 30 years, same code, only `CohabitOnPairing` changed:

| Metric | Off (default) | On |
|---|---:|---:|
| Population | 199 | 219 |
| Births / deaths | 75 / 111 | 83 / 86 |
| Housing vacancy | 17.7% | 26.6% |
| Overcrowded units | 3 | 9 |
| Homeless households | 0 | 0 |
| Median rent burden | 0.1891 | 0.1797 |
| Wealth Gini | 0.5878 | 0.5683 |
| Money supply | 1,908,629,300c | 1,915,454,300c |
| Tick p95 | 2.73 ms | 3.96 ms |

`CohabitOnPairing` default: **not flipped**. Cohabitation improves population
and births, but housing vacancy rises from 17.7% to 26.6% (already above the 10%
warn band) and overcrowded units triple. That is a housing-pressure regression,
not a green before/after result. The comparison is recorded for feature 03
tuning; the default stays off.

## 7. Performance

| Scenario | p50 | p95 | p99 | Alloc/tick | Status |
|---|---:|---:|---:|---:|---|
| 2,000 sims, 10 years | ~2.4 ms | 35.6 ms | 66.7 ms | ~3.1 MB | Pass (p95 cap 100 ms) |
| 2,000 sims, animated commute year | 6.28 ms p95 vs 6.42 ms abstract | — | — | — | Pass (15 ms guardrail, +2 ms vs abstract) |
| 32 players, social soak | 0.16 ms | 0.88 ms | 3.5 ms | 27.6 KB | Pass (cap 100 ms) |
| Appearance composition, 2,000 descriptors | — | 0.99 ms total | — | — | Pass (< 16.7 ms) |
| 1k / 5k synthetic | 0.24 / 1.26 ms | 0.32 / 11.95 ms | 3.95 / 18.46 ms | 0.26 / 1.35 MB | Pass |
| 1k / 5k realistic | 0.23 / 1.09 ms | 0.55 / 9.71 ms | 4.91 / 14.48 ms | 0.26 / 1.35 MB | Pass |
| Restore then 1,000 ticks | — | — | — | — | Pass (release restore check) |
| Live preview 30-minute soak | — | — | — | — | **Exception: not run** |

The 2k p99 is GC-driven (p50 stays ~2.4 ms); the p95 acceptance budget is the
release gate. The final full-suite run measured p95 35.6 ms and p99 66.7 ms. The 2k decade allocates ~3.1 MB/tick and the worst system is
`RelationshipExposure`.

## 8. Migration and rollback

- Schema 11 → 15 migration: Pass in persistence tests; a real pre-V5 backup
  rehearsal is still required before production.
- Restore and double restore: Pass (`restoredPopulation` matches capture;
  1,841 living sims restored in the 2k release run).
- Player, appearance, consumption and social state: Pass in suite.
- Chat retention does not block restore: Pass.
- Rollback rehearsal: **Exception: use the previous build + pre-upgrade backup;
  not rehearsed against a production-like deployment**.
- Pre-migration snapshot backup: **Operator action at release**.

## 9. Live preview

- Seeded deployment: Pass. `tools/preview/reset.sh` starts a fresh map and
  generates the fixed 200-sim world used by the smoke.
- Player, appearance, consumption, social and admin smoke: Pass (5 player/read
  model tests, 1 admin page sweep).
- Reconnect and recovery: Pass (network interruption test; server ping timeout
  is accounted for).
- Monitoring and alerts: `/api/simulation/metrics` and browser HUD verified;
  alert delivery is deployment configuration, not exercised locally.
- Screenshots and logs: Playwright traces in `clients/browser/test-results*`;
  `[metrics]` lines in the API log.
- Input-to-ack: pointer p50 40 ms / p95 51 ms (5 samples, local seeded
  preview). Touch was 109 ms on one emulated tap and 149–603 ms across repeats,
  coinciding with a `Pathfinding` max of 1.15–1.47 s; the touch path is a
  profiling trigger, not a prediction case. Decision: no prediction (see
  `prediction-decision.md`).

## 10. Known gaps and required actions

- Same-sex pairing and reproduction: policy decision, unchanged.
  - Action: product owner records the policy decision; no engineering work until
    the policy is signed off. Not a preview blocker.
- Divorce, custody, adoption and surrogacy: deferred.
  - Action: schedule in a future roadmap; no V5 work.
- Richer genome and genetic disease model: deferred.
  - Action: schedule in a future roadmap; no V5 work.
- Player-owned businesses, utilities and grocery chains: deferred.
  - Action: schedule in a future roadmap; no V5 work.
- Voice chat, groups and social feed: deferred.
  - Action: schedule in a future roadmap; no V5 work.
- Balance miss: 1k median rent burden 0.4284 vs 0.40; 2k passes at 0.3464.
  - Action: balance owner tunes 1k rent/income parameters or formally accepts the
    miss, then re-runs the balance scenario. Required before production sign-off.
- Overcrowding warnings: the 2k family release run reported 45 child-level
  `housing.overcrowded` warnings for 18 distinct overcrowded households. The
  invariant now deduplicates per household, so the same run reports 18 warnings
  (19 overcrowded units in the balance run). Not a high-severity failure; the
  remaining warnings are real overcrowding, documented, not hidden.
  - Action: reporting bug fixed in feature 05 (one warning per household).
    Eliminating the remaining 18 warnings requires feature 03 housing economics
    tuning (vacancy/affordability); track as a follow-up. Not a preview blocker.
- `CohabitOnPairing` remains opt-in after the 30-year comparison: population/births
  rise, but housing vacancy 17.7% → 26.6% and overcrowded units 3 → 9.
  - Action: keep opt-in for V5 and revisit after housing vacancy/overcrowding
    tuning. No V5 change.
- 30-minute live preview soak and production migration/rollback rehearsal.
  - Action: release owner runs the 30-minute soak and the pre-V5 backup
    migration/rollback rehearsal, then records the results here. Required before
    production sign-off.

## 11. Sign-off

- Release owner: TBD
- Reviewer: TBD
- Date: TBD
- Commit: TBD
- Rollback build: previous V5 build + pre-upgrade snapshot
- Decision: **Conditional go for preview only**. Production sign-off requires
  the 30-minute soak, the pre-V5 backup rehearsal and a reviewer.

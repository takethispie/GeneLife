# Feature 05 — Release Evidence

**Status:** Not recorded yet — populate at V5 release
**Verdict:** Pending
**Date:** TBD
**Commit:** TBD

This file is the evidence bundle for the V5 release. Each section must point to a test, a scenario report, a deployment smoke or a signed decision. Do not mark a section complete from intent alone.

## 1. Build and tests

| Check | Result | Evidence |
|---|---|---|
| `dotnet build GeneLife.sln` | Pending | Build log |
| Full PostgreSQL suite | Pending | Test report |
| Player suite | Pending | Test report |
| Appearance suite | Pending | Test report |
| Consumption suite | Pending | Test report |
| Social suite | Pending | Test report |
| Persistence suite | Pending | Test report |
| Performance suite | Pending | Test report |
| Client typecheck/lint/test/build | Pending | CI report |
| Client E2E and live suite | Pending | Playwright report |

## 2. Player life

- Job → home → consumption → family → death → continuation: Pending.
- Replay with recorded intent log: Pending.
- 32-player soak: Pending.
- Server authority and audit: Pending.

## 3. Appearance

- Determinism and inheritance: Pending.
- Family resemblance and aging: Pending.
- No per-sim textures: Pending.
- Payload and frame budget: Pending.
- Admin preview: Pending.

## 4. Consumption

- Utility networks, load and state: Pending.
- Metered billing, arrears and outages: Pending.
- Grocery stock, prices and spoilage: Pending.
- Nutrition effects: Pending.
- Conservation: Pending.
- Admin page: Pending.

## 5. Social

- Presence scopes and privacy: Pending.
- Chat, emotes and retention: Pending.
- Friends, blocks, mutes and reports: Pending.
- Player partnerships and cohabitation: Pending.
- Venues and activities: Pending.
- Moderation queue and audit: Pending.
- 32-player soak: Pending.

## 6. Balance and cohabitation

| Metric | V4 baseline | V5 result | Target | Status |
|---|---:|---:|---:|---|
| Wage share | 0.9078 | Pending | Band | Pending |
| Vacancy | 0.0854 | Pending | Band | Pending |
| Company failures | Pending | Pending | Band | Pending |
| Wealth Gini | Pending | Pending | Target | Pending |
| 30-year continuity | Green | Pending | No new `Fail` | Pending |
| Overcrowded | 1 | Pending | Report | Pending |
| Health warnings | 4 | Pending | Report | Pending |

`CohabitOnPairing` default: Pending decision.

## 7. Performance

| Scenario | p50 | p95 | p99 | Alloc/tick | Status |
|---|---:|---:|---:|---:|---|
| 2,000 sims, 10 years | Pending | Pending | Pending | Pending | Pending |
| 32 players, social soak | Pending | Pending | Pending | Pending | Pending |
| Appearance composition | Pending | Pending | — | Pending | Pending |
| Live preview 30-minute soak | Pending | Pending | Pending | Pending | Pending |

Record the hardware, browser and network used.

## 8. Migration and rollback

- Schema 11 → 15 migration: Pending.
- Restore and double restore: Pending.
- Player, appearance, consumption and social state: Pending.
- Chat retention does not block restore: Pending.
- Rollback rehearsal: Pending.
- Pre-migration snapshot backup: Pending.

## 9. Live preview

- Seeded deployment: Pending.
- Player, appearance, consumption, social and admin smoke: Pending.
- Reconnect and recovery: Pending.
- Monitoring and alerts: Pending.
- Screenshots and logs: Pending.

## 10. Known gaps

- Same-sex pairing and reproduction: Pending policy decision.
- Divorce, custody, adoption and surrogacy: Deferred.
- Richer genome and genetic disease model: Deferred.
- Player-owned businesses, utilities and grocery chains: Deferred.
- Voice chat, groups and social feed: Deferred.
- Balance target misses: Pending final numbers.
- Prediction: Pending decision.

## 11. Sign-off

- Release owner: TBD
- Reviewer: TBD
- Date: TBD
- Commit: TBD
- Rollback build: TBD
- Decision: Pending

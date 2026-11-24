# Feature 05 — Release Checklist

**Feature:** Live operations and scale
**Status:** Implementation complete; release sign-off pending exceptions listed in `release-evidence.md`
**Date:** 2026-11-23 (plan)

This checklist is the V5 release gate. Every item must be checked or carry a documented exception with a reason and a follow-up owner. Automated evidence is in `release-evidence.md`; unchecked items below are the remaining live/manual gates, not untested code.

## 1. Build and unit gate

| Check | Command | Expected |
|---|---|---|
| Solution build | `dotnet build GeneLife.sln` | Pass |
| Fast domain suite | `dotnet run --project Genelife.Domain.Tests/Genelife.Domain.Tests.csproj --no-launch-profile -- -trait- "Category=Scenario"` | 0 failed |
| API integration suite | `docker compose up -d postgres && dotnet run --project Genelife.Api.IntegrationTests/Genelife.Api.IntegrationTests.csproj --no-launch-profile` | 0 failed |
| Player suite | `dotnet run --project Genelife.Domain.Tests/Genelife.Domain.Tests.csproj --no-launch-profile -- -class "Genelife.UnitTests.Player.*"` | Pass |
| Appearance suite | `dotnet run --project Genelife.Domain.Tests/Genelife.Domain.Tests.csproj --no-launch-profile -- -class "Genelife.UnitTests.Appearance.*"` | Pass |
| Consumption suite | `dotnet run --project Genelife.Domain.Tests/Genelife.Domain.Tests.csproj --no-launch-profile -- -class "Genelife.UnitTests.Consumption.*"` | Pass |
| Social suite | `dotnet run --project Genelife.Domain.Tests/Genelife.Domain.Tests.csproj --no-launch-profile -- -class "Genelife.UnitTests.Social.*"` | Pass |
| Persistence suite | `dotnet run --project Genelife.Domain.Tests/Genelife.Domain.Tests.csproj --no-launch-profile -- -class "Genelife.UnitTests.Persistence.*"` | Pass |
| Performance and scenarios | `dotnet run --project Genelife.Performance.Tests/Genelife.Performance.Tests.csproj --no-launch-profile` | Pass |
| Client | `cd clients/browser && npm run typecheck && npm run lint && npm run test && npm run build` | Pass |
| Client E2E | `npm run test:e2e` and the live suite | Pass |

## 2. Player life gate

- [ ] A new player gets a valid sim, profile, wallet, needs and household.
- [ ] Job application, hiring, attendance, payroll and quitting work live.
- [ ] Rent, arrears, eviction and moving work live.
- [ ] Buying and consuming food works and affects needs.
- [ ] Player partnership, conception, birth and child naming work live.
- [ ] Player death, estate settlement and heir continuation work live.
- [ ] Every player action is server-authoritative, rate-limited and audited.
- [ ] No player route exposes another player's private state.

## 3. Appearance gate

- [x] Every living sim has a deterministic appearance descriptor.
- [x] `SkinTone` and `FaceShape` migrate deterministically.
- [x] Family resemblance and aging are visible in the browser.
- [x] The client uses shared atlas parts and palettes, not per-sim textures.
- [x] The admin `/appearance` preview matches the browser identity.
- [x] Payload and frame budgets pass.

## 4. Consumption gate

- [x] Water and electricity networks have production, capacity, load and state.
- [x] Households have meters and metered bills.
- [x] Arrears lead to reduced service and cut-off with a visible cause.
- [x] Grocery has catalog, stock, prices, restock and spoilage.
- [x] Nutrition affects health and needs without replacing them.
- [x] Money and goods are conserved.
- [x] The admin `/consumption` page works.

## 5. Social gate

- [x] Presence is scoped to nearby, venue and friends.
- [x] Local chat, venue chat and whispers work with rate limits and retention.
- [x] Emotes are predefined and cooldown-limited.
- [x] Friend requests, blocks, mutes and reports work in both directions.
- [x] Player partnerships require invitation and acceptance.
- [x] Cohabitation is a separate consent and respects capacity.
- [x] Moderation can warn, mute, block, remove a message, close a venue and suspend chat.
- [x] No real names, emails or IP addresses are persisted.
- [x] Chat retention does not block restore.

## 6. Admin smoke test

1. Start the API, web app and browser client against the seeded preview. Automated pages: `tools/preview/reset.sh` + `E2E_ADMIN_LIVE=1`.
2. Open `/player` and inspect a player's job, home, family, actions and continuation.
3. Open `/appearance` and compare a family.
4. Open `/consumption` and inspect networks, meters, outages, stock and nutrition.
5. Open `/social` and inspect presence, reports, blocks, friendships and moderation.
6. Open `/family` and confirm the V4 page still works.
7. Confirm every destructive action requires confirmation.
8. Confirm every admin route returns 401/403 without the admin policy.
9. Confirm player routes never expose another player's private state.

## 7. Performance evidence

| Scenario | Expected |
|---|---|
| 200 sims, 30 years, continuity | Pass, 199 living, 75 births, 111 deaths, 0 orphans, 0 fails |
| 1k sims, 10 years, balance | Report: rent burden 0.4284 (miss), all other reported bands green |
| 2k sims, 10 years, balance and tick | Pass: p95 35.6 ms, 0 health failures, 1,937 living, 0.3464 rent burden |
| 2k sims, appearance composition | 0.99 ms total, inside 16.7 ms |
| 32 players, social soak | Pass: p95 0.88 ms, no message loss or duplicate edges |
| Restore then 1,000 ticks | Pass |
| Live preview 30-minute soak | **Exception: not run** |

Record tick p50/p95/p99, allocation per tick, snapshot/delta bytes, frame time, command drain cost, projection lag and the worst system.

## 8. Migration runbook (schema 11 → 15)

Schema 12 adds player state; 13 adds appearance genes; 14 adds utilities/grocery/nutrition; 15 adds social state.

1. Stop the simulation and allow the current snapshot to finish.
2. Back up the SQLite snapshot file and the PostgreSQL Marten schema.
3. Deploy the new build.
4. Restore the snapshot. `SnapshotMigrations.Upgrade` applies schema 12–15 in order.
5. Confirm the engine starts and all health checks pass.
6. Confirm player state, appearance, meters, stock, nutrition and social edges restore.
7. Confirm chat and moderation documents are outside the world snapshot.
8. Run the player, appearance, consumption, social and persistence suites against the migrated save.
9. Run a 1-year simulation and a restore.

**Rollback:** schemas 12–15 are additive. Stop the app, restore the pre-upgrade snapshot backup and redeploy the previous build. Do not edit `CurrentSchemaVersion` manually. Chat and moderation documents remain separate.

**Failure policy:** missing player profiles, undefined appearance enums, missing utility networks, negative stock and duplicate social edges fail loudly. A legacy save with no social data gets empty edges, not invented ones.

## 9. Observability

- `GET /api/player` — player read model and action audit.
- `GET /api/appearance` — descriptor, version and cache state.
- `GET /api/consumption` — networks, meters, outages, stock and nutrition.
- `GET /api/social` — presence, reports, edges and moderation.
- `GET /api/family` — V4 family read model.
- `GET /api/simulation/metrics` — tick, loop, replication and scaling.
- Marten documents for player, appearance, consumption, social, chat, reports and moderation.
- Alerts on invariant failures, tick overrun, projection lag and report backlog.

## 10. Known gaps

- Same-sex pairing and reproduction remain a policy decision.
- Divorce, custody, adoption and surrogacy remain deferred.
- A richer genome and genetic disease model remain deferred.
- Player-owned businesses, utilities and grocery chains remain deferred.
- Voice chat, groups and a social feed remain deferred.
- Wealth Gini, vacancy and company failures may remain outside target; record the final numbers.
- Prediction is not implemented; pointer p95 51 ms and touch outliers traced to a slow pathfinding tick supported a no-go decision.

## 11. Acceptance

- Every checklist item is checked or has a documented exception.
- The release report, migration runbook and rollback plan are reviewed.
- No silent default change affects an existing save.
- No player, appearance, consumption, social or family invariant is knowingly failing.
  The 2k family run has zero `Fail` results; its 18 `housing.overcrowded` warnings
  (one per distinct household, down from 45 child-level warnings) are documented
  in the evidence.
- Sign-off is recorded with commit id and date. **Pending** the 30-minute live
  soak, pre-V5 backup/rollback rehearsal and reviewer.

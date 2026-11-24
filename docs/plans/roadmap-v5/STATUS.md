# Roadmap V5 — Status

**Status:** Features 01–02 complete; feature 03 core complete; feature 04 presence/directory implemented; remaining 04–05 planned
**Builds on:** V1 economy/housing/life stages, V2 spatial world, V3 browser client, V4 traits/relationships/reproduction/inheritance
**Scope:** player life and household, appearance and visual identity, consumption and utilities, social and multiplayer, live operations and scale
**Primary risk:** player-facing systems touch the economy, housing, family graph, persistence and client at the same time; a small convenience shortcut can break server authority or money conservation

## Delivery checklist

| Area | Status | Planned evidence |
|---|---|---|
| Player agency and command contract | Done (feature 01) | Typed player intents, tick-start drain, rate limits, command results, no client authority |
| Player employment and income | Done (feature 01) | Job application/accept/quit, wages, attendance, career, ledger entries |
| Player housing and household | Done (feature 01) | Rent, move, household membership, arrears, eviction, capacity rules |
| Player consumption and needs | Done (feature 01) | Shopping, wallet debits, needs effects, UBI remains the only recurring mint |
| Player relationships and family actions | Done (feature 01) | Invitations, consent, partnership, pregnancy/birth paths, NPC consent policy |
| Player aging, death and heir continuation | Done (feature 01) | Age progression, death, estate, heir selection, continuation policy |
| Player persistence and admin page | Done (feature 01) | Schema 12, player profile/read model, `/player` admin page, action audit |
| Player life acceptance | Done (feature 01) | Recorded-command replay from snapshot and a 32-player 100-tick soak are green alongside unit/persistence tests |
| Appearance descriptor model | Done (feature 02) | `SkinTone`, `FaceShape`, deterministic descriptor from traits/age/sex |
| Appearance derivation and inheritance | Done (feature 02) | Child resembles parents, bounded variation, replay test |
| Portrait and sprite composition | Done (feature 02) | Layered atlas, palette/tint, pooled sprites, no per-sim textures |
| Aging and family resemblance | Done (feature 02) | Age-band visuals, hair change, proportions, family comparison |
| Client asset pipeline and rendering | Done (feature 02) | Asset manifest, preload, render integration, fallback |
| Appearance persistence and admin preview | Done (feature 02) | Schema 13, backfill, `/appearance` preview, cache invalidation |
| Appearance acceptance | In progress (feature 02) | Descriptor/inheritance/aging/payload tests green; Playwright visual smoke and hardware frame-budget report remain |
| Consumption and utility domain model | Done (feature 03) | Networks, service links, meters, service levels, conservation rules |
| Utility networks and capacity | Done (feature 03) | Water/electricity production, load, capacity, priority, outage risk |
| Household utility billing and outages | Done (feature 03) | Metered billing, arrears, outage effects, ledger and events |
| Grocery catalog, stock and pricing | Done (feature 03) | Catalog, store inventory, restock, deterministic pricing, spoilage |
| Shopping trips and nutrition | In progress (feature 03) | Player catalog purchase/consume, nutrition state and monthly health modifier are live; NPC shopping trips and meal composition remain |
| Economy recalibration and cohabitation | In progress (feature 03) | Municipality-funded utility tariffs keep 60/200-sim bands green; seeded-pair cohabitation and the 30-year before/after evidence remain |
| Consumption persistence and admin page | Done (feature 03) | Schema 14, `/consumption` page, audited repair/restock and yearly `ConsumptionMetricsRecorded` metrics are live |
| Consumption acceptance | In progress (feature 03) | Conservation, network, grocery, balance and schema-14 tests green; long outage/30-year scenarios remain |
| Presence and player directory | Done (feature 04) | Nearby/same-venue scopes, privacy, reconnect, admin list and client directory panel; disconnect filtering is immediate |
| Chat and emotes | Planned (feature 04) | Text chat, emotes, rate limits, retention, mute/block |
| Social graph | Planned (feature 04) | Friendship edges, blocks, reports, idempotent projections |
| Consensual player relationships | Planned (feature 04) | Invitations, accept/decline, partnership edge, no automatic wallet/household merge |
| Shared venues and activities | Planned (feature 04) | Venue presence, proximity interactions, activity events |
| Moderation and abuse | Planned (feature 04) | Reports, admin queue, audit, retention, appeal notes |
| Social persistence and admin | Planned (feature 04) | Schema 15, `/social` page, privacy and retention tests |
| Social acceptance | Planned (feature 04) | 32-player soak, abuse/rate-limit tests, reconnect behavior |
| Hybrid Wolverine cleanup | Planned (feature 05) | H16/H19/H20 closed, feature flags removed, parity verified |
| Command/event/status reconciliation | Planned (feature 05) | MP05 and roadmap docs match code; command audit complete |
| Building relocation and commute animation | Planned (feature 05) | Occupied-building relocation, path cache refresh, `CommuteMode.Animate` default |
| Live preview and browser acceptance | Planned (feature 05) | Seeded preview deployment, live Playwright suite, hardware render numbers |
| Audio and touch controls | Planned (feature 05) | Minimal audio cues, mobile/touch scheme, settings |
| Prediction decision and implementation | Planned (feature 05) | Measured latency report, go/no-go, optional implementation |
| 1k–2k balance and performance | Planned (feature 05) | Wage share, vacancy, failures, Gini report; 2k decade and 32-player soak |
| Release checklist and evidence | Planned (feature 05) | Full release checklist, migration runbook, rollback and sign-off |

## Verification plan

| Check | When |
|---|---|
| `dotnet build GeneLife.sln` | every server tech step |
| Relevant unit-test namespace | every server tech step |
| `dotnet run --project Genelife.Domain.Tests/Genelife.Domain.Tests.csproj --no-launch-profile -- -trait- "Category=Scenario"` | every feature completion |
| `dotnet run --project Genelife.Api.IntegrationTests/Genelife.Api.IntegrationTests.csproj --no-launch-profile` | every API or persistence change |
| `BalanceScenarioTests` 10-year report | before and after V5 economy/consumption changes |
| 30-year continuity scenario | after feature 03 and before flipping `CohabitOnPairing` |
| 2,000-sim decade | after feature 01 and at release |
| 32-player social soak | after feature 04 and at release |
| Schema 11 → 15 migration, restore and double restore | after feature 05 persistence work |
| Player-life replay with recorded intent log | after feature 01 |
| Appearance determinism and frame budget | after feature 02 |
| Utility/grocery conservation | after feature 03 |
| Chat retention, block and report tests | after feature 04 |
| `npm run typecheck`, `npm run lint`, `npm run test`, `npm run build` | every client tech step |
| Live preview Playwright suite | feature 05 |
| Admin smoke test for `/player`, `/appearance`, `/consumption`, `/social` | feature 05 |
| Full PostgreSQL suite | feature 05 release |

## Current baseline

- `PersistedWorldState.CurrentSchemaVersion` is 14. Feature 03 adds metric utility networks, links, household meters, service levels, tariff billing, grocery catalog/inventory/pricing/spoilage/nutrition, schema 13→14 migration with conservative bootstrapping, and the `/consumption` admin page. Default tariffs are municipality-subsidised (100%) so the strict rent-burden and unemployment bands stay green; setting `SubsidyBasisPoints = 0` exercises household payment, arrears and cut-off.
- The player sim is created on connect and still uses `MoveIntent`/`UseBuildingIntent` for movement and building use. Typed `PlayerActionIntent` commands now cover job application/accept/quit, housing request/lease/move, arrears, shopping, consumption, partner invitations and consent, child naming, and heir/new-sim/observer continuation. Player sims remain excluded from autonomous decisions, job seeking and work scheduling.
- V4 family systems are complete for NPCs: traits, relationship edges, eligibility, attraction, pairing, pregnancy, birth, childhood, guardianship, inheritance and the admin family page.
- The browser client renders map, buildings and sims. Sims now compose layered shared-part bodies, heads, eyes, hair and outfit palettes with local/other-player markers; the admin `/appearance` page renders the same layer contract as SVG and compares a family.
- The world supports up to 32 concurrent players with server-authoritative presence, snapshots, deltas, viewport filtering and reconnect. There is no chat, social graph or moderation.
- Utilities are a single abstract bill from `HousingCostPolicy`; there are no water/electricity networks, meters or outages. Grocery is minimal with no stock, pricing, spoilage or nutrition.
- The hybrid ECS + Wolverine/Marten architecture is in place. H16, H19 and H20 are partial; MP05 is marked partial in docs even though `SimulationEngine.DrainPlayerIntents()` applies player intents.
- The server suite is 577 total / 0 failed / 2 skipped; the browser suite is 154 tests green with typecheck, lint and build passing. Feature 03 tests cover utility network states/priority shedding, consumption conservation, tariff validation, grocery pricing/stock-out/restock/spoilage, player purchase conservation, nutrition health modifiers, and schema-14 capture/restore/double restore. The 60- and 200-sim ten-year balance bands are green with utility and grocery systems enabled.
- V1's 1k–2k balance gaps remain open: vacancy, company failures, wealth Gini and wage share are reported but not fully closed.

## Decisions to confirm before implementation

1. **Same-sex pairing and reproduction.** The options exist but default to false. Decide the policy before enabling player or NPC paths; update eligibility, conception, tests and docs together.
2. **Player death and continuation.** Choose heir continuation, respawn or observer mode, and define what happens to property, partnerships and debts.
3. **Player–NPC family consent.** Define a deterministic consent policy that cannot be coerced by repeated requests.
4. **Appearance scope.** Confirm `SkinTone` and `FaceShape` as the only new trait fields and trait-derived appearance as the V5a scope; no character creator.
5. **Utility ownership.** V5a assumes one authoritative network per service. Decide whether utilities are government-owned, company-owned or a regulated market later.
6. **Grocery pricing.** V5a assumes a deterministic store catalog. Decide whether companies or players can set prices later.
7. **Chat retention and moderation.** Define retention, report/appeal path, admin access and privacy rules before feature 04 ships.
8. **Social scope.** Confirm that player friendships and player partnerships are in V5, or cut feature 04 to chat/presence only.
9. **Prediction.** Decide from measured latency whether own-player prediction is needed; do not implement it speculatively.

## Known risks

- **Server authority.** Player actions are the easiest place to accidentally let the client mutate balances, positions or family state. Every action must be a typed intent applied on the tick thread.
- **Money conservation.** Player jobs, rent, shopping, utilities and inheritance all move money. A single unledgered path breaks the closed loop.
- **Economy calibration.** Cohabitation and utility/grocery consumption change household budgets at the same time. The strict bands and the 30-year run must stay green before the default flips.
- **Appearance cost.** A naive per-sim texture or per-trait portrait cache can blow up memory and frame time. Layered atlas composition and pooling are mandatory.
- **Social abuse.** Chat and player relationships need rate limits, blocks, reports and retention from the first release, not after an incident.
- **Persistence breadth.** Schema 12–15 adds player, appearance, utility, grocery and social state. Migration and double-restore must be exact, and chat must not make a world snapshot unrestorable.
- **Scope creep.** Feature 04 can grow into a social platform. V5a keeps text chat, presence, friendships and consensual partnerships; everything else is later.
- **Stale documentation.** MP05 and some roadmap status text already disagree with the code. Feature 05 must reconcile docs rather than carrying the contradiction into V5.

## Next

1. Confirm remaining decisions, especially same-sex policy, the continuation default and chat retention.
2. Add the recorded-intent replay test and 32-player player-life soak for feature 01 acceptance.
3. Add Playwright appearance visual smoke and a reference-hardware frame-budget report, then finish feature 02 acceptance.
4. Finish feature 03 NPC shopping trips, meals and health modifiers, then run the cohabitation before/after 30-year evidence before flipping `CohabitOnPairing`.
5. Keep feature 04 behind a flag until moderation and retention are ready.
6. Treat feature 05 as the release gate: no live preview, balance or migration evidence means no V5 release.

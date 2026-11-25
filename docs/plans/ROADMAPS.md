# Roadmaps — Resume

Consolidated summary of the roadmaps in this folder. Each roadmap has its own `README.md` (plan) and `STATUS.md` (delivered state); this file is the quick-read index.

| Roadmap | Theme | Status |
|---|---|---|
| [V1](roadmap-v1/README.md) | Realistic population simulation (economy, housing, life stages) | Implemented |
| [V2](roadmap-v2/README.md) | Spatial world, buildings and player presence | Implemented |
| [V3](roadmap-v3/README.md) | Browser game client | Implemented (vertical slice) |
| [V4](roadmap-v4/README.md) | Relationships, attraction and reproduction | Release-ready (one opt-in follow-up) |
| [V5](roadmap-v5/README.md) | Playable society (player life, appearance, consumption, social, live ops) | Features 01–04 done; feature 05 implementation complete, sign-off pending |
| [V6](roadmap-v6/README.md) | Tick performance (dropped ticks, lock contention, parallelism) | Implemented |

---

## V1 — Realistic Population Simulation

**Scope:** backend + admin web only. No game engine or client.
**Target:** NPCs that earn, spend, rent, age, retire and die inside the existing closed economy.

- **Features:** economy (money, offices, postings, payroll), housing (households, rent, homelessness), life stages (education → work → retirement → elderly, death and estates).
- **Key decisions:** integer-cent `Money`; 1,200 ticks/year; command path for admin mutations; derived indices (`EmploymentIndex`, `HousingRegistry`, `LifecycleIndex`); collect → decide → apply; `SimulationRandom` streams.
- **Delivered:** company accounts, ledger, payroll, arrears, revenue, tax, pensions, credit, insolvency, savings; households, rent, utilities, eviction, construction, ownership; aging, schools/skills, work/attendance/careers, unemployment, pensions, health/mortality, estates, immigration, generator, metrics — all with admin API/UI.
- **Verification:** 329 passed / 2 skipped; 1k/2k/5k performance guardrails; restore coverage incl. legacy snapshots and a 10-year scenario.
- **Open:** 1k–2k balance tuning (vacancy, company failures, wealth Gini); lifecycle page extensions.

## V2 — Spatial World, Buildings and Player Presence

**Scope:** backend + admin map console. No rendering pipeline.
**Target:** every building has a validated footprint on a deterministic tile map; every human-controlled sim has a server-authoritative position and replication contract.

- **Features:** world map (coordinates, generation, placement, occupancy), player movement (spawn, intent drain, movement, pathfinding), map replication (static map, deltas, AOI, admin console).
- **Key decisions:** integer tile coordinates centred on origin (256×256 default); `TilePosition` authoritative, `Location` derived; `WorldMap`/`MovementIndex` derived indices; static layout vs dynamic state split; schema 4 → 5.
- **Delivered:** deterministic generation, occupancy/placement service, building bootstrap, admin placement, persistence/restore + V4 migration, map read API, admin map console, player spawn/intent drain, movement/pathfinding, NPC commute presence, building interactions, map events/projections, AOI/viewport.
- **Verification:** 341 passed / 2 skipped; V1 suite green.
- **Deferred:** occupied-building relocation (`RelocateBuildingCommand`); `CommuteMode.Animate` default-on with a 2,000-sim budget test; small map history retention.

## V3 — Browser Game Client

**Scope:** browser-only 2D client on the V2 server. No Godot/native client.
**Target:** a playable browser client — deterministic map, nearby sims/buildings, server-authoritative movement, reconnects, HUD.

- **Features:** client foundation, world rendering (PixiJS), realtime presence (SignalR, interpolation, viewport, reconnect), input and HUD, hardening and release.
- **Key decisions:** TypeScript + Vite + PixiJS + SignalR + DOM HUD; three stores (simulation 25 Hz / session / UI); RLE map decoding; 120 ms interpolation delay; viewport debounce 200 ms; no client authority.
- **Delivered:** full client in `clients/browser` — auth, config, RLE decoder, map cache, Pixi scene, camera, replication store, interpolation, viewport sync, input/intents, DOM HUD, notifications, settings, debug overlay, 25 Vitest files (148 tests), Playwright fixture/live suites, CI, soak script, Aspire AppHost.
- **Verification:** 358 server tests / 0 failed; 148 client tests; fixture Playwright 3/3; live Playwright passing.
- **Known gaps:** no own-player prediction (V5 measured p95 51 ms, no-go); touch/audio/chat/social/accounts UI exist but matchmaking out of scope; silent network loss detected by server ping timeout (tens of seconds); full 2,000-sim GPU frame rate environment-dependent.

## V4 — Relationships, Attraction and Reproduction

**Scope:** server simulation, persistence, read models, admin surfaces. No appearance rendering or player pregnancy UI.
**Target:** deterministic inherited traits; explainable attraction; partnerships, households, children; births as a continuity path; inheritance; family graph observability.

- **Features:** traits/genetics, relationships/attraction, reproduction/family, continuity/inheritance, admin/observability/hardening.
- **Key decisions:** `Traits` record struct (byte-backed enums); directed relationship edges (Parent/Child/Partner/ExPartner/Guardian/Ward); integer basis-point attraction score; opposite-sex pairing only; births supplement immigration; schema 5 → 11.
- **Delivered:** traits + inheritance/mutation; relationship edges/index, eligibility, scorer, pairing, capacity-aware merge; pregnancy/birth/newborn factory, childhood gates, guardian-paid child consumption; widowhood/guardianship, relationship-based inheritance with minor trusts, housing transfer; family read model, `Family.razor`, Marten projections, schema 11 persistence, decade replay, seed sweep, 2,000-sim ten-year run (p95 ~3.1 ms), 30-year continuity.
- **Verification:** full PostgreSQL suite 531 / 0 failed / 2 skipped; admin `/family` Playwright coverage.
- **Open:** `CohabitOnPairing` stays opt-in — 30-year comparison (200 sims, seed 21): population 199→219, births 75→83, but vacancy 17.7%→26.6% and overcrowded units 3→9. Pending economy/consumption recalibration. Non-blocking warnings: 61 health warnings (2k ten-year), 4 reproduction `Warn` levels (30-year).

## V5 — Playable Society

**Scope:** server simulation, player-facing browser client, persistence, read models, admin surfaces. No native client, no real-money economy.
**Target:** a player lives a full life in the town — job, money, home, family, appearance, water/electricity/grocery constraints — while the economy stays stable at 1k–2k sims and 32 players stay server-authoritative.

- **Features:** player life/household, appearance/visual identity, consumption/utilities, social/multiplayer, live operations/scale.
- **Key decisions:** player actions as typed intents drained at tick start; appearance derived from traits/age/sex (`SkinTone`, `FaceShape`); utility networks + grocery catalog/stock/nutrition; text chat + presence + friendships + consensual partnerships; schema 11 → 15.
- **Delivered:** player agency (jobs, housing, consumption, family actions, aging/death/heir continuation, schema 12, `/player` admin page); appearance (descriptor model, inheritance, layered sprites, `/appearance` preview, schema 13); consumption (networks, meters, tariffs, grocery, nutrition, schema 14, `/consumption` page); social (presence, chat/emotes, friendships/blocks/reports, player partnerships, venues, moderation, schema 15, `/social` page); live ops (Wolverine cleanup H16/H19/H20, relocation, live preview, audio/touch, prediction no-go, balance).
- **Verification:** 560 domain + 69 API integration + 33 performance/release tests green; 178 browser tests; 2k decade p95 35.6 ms; 32-player p95 0.88 ms; 60/200-sim ten-year bands green.
- **Open / pending sign-off:** 30-minute live soak; schema 11→15 migration/rollback rehearsal; feature 02 Playwright visual smoke + hardware frame-budget report; feature 03 NPC shopping trips/meals + cohabitation comparison; 1k rent burden 0.4284 vs 0.40 ceiling (documented miss); `CohabitOnPairing` still opt-in.

## V6 — Tick Performance

**Scope:** server simulation loop only. No gameplay/client/persistence changes.
**Target:** eliminate dropped ticks by cutting per-tick cost and lock contention; 20 Hz loop inside its 50 ms budget at 2,000 sims and under 32-player load.

- **Features:** runtime/hot-path trims (Tier 1), lock/cadence structure (Tier 2), architecture/parallelism (Tier 3).
- **Key decisions:** server GC/concurrent GC/tiered PGO/R2R; throttled overrun logging; indexed company snapshots; appearance cache; pooled event lists; `ReaderWriterLockSlim` replacing global lock; cadence `Offset` (opt-in); `TryGet` dedupe; sampled per-system timing; pathfinding iteration cap; dedicated sim thread; parallel per-entity passes (opt-in); async snapshot build.
- **Delivered:** all Tier 1–3 changes landed. Two deliberately opt-in: cadence staggering (breaks balance bands — 0.4369 vs 0.40 rent burden) and parallel per-entity passes (5k p95 6.4→11 ms in Debug). Merged entity passes rejected (DecisionSystem ordering). Legacy admin mutations now go through typed admin commands.
- **Measurements:** 1k p95 2.74→0.20 ms; 5k p95 6.43→5.95 ms; allocation roughly flat (dominated by economy systems). 32-player soak passes.
- **Open:** re-measure opt-ins in Release build; legacy admin endpoints wait up to 50 ms for the next tick (could move to 202 + command-id pattern).

---

## Cross-roadmap themes

- **Server authority everywhere:** the browser never mutates the world; typed intents/commands drained at tick start under the write lock.
- **Determinism:** `Random.Shared` banned; `SimulationRandom` named streams; stable iteration order; replay tests at every stage.
- **Money conservation:** integer-cent `Money`; every transfer ledgered; balance bands (rent burden, vacancy, Gini, company failures) reported at 1k/2k.
- **Collect → decide → apply:** no entity mutation inside query callbacks; indices rebuilt on restore.
- **Admin-first observability:** every feature ships read models, endpoints and Blazor pages in the same PR.
- **Schema discipline:** V1→V5 advanced persistence from schema 4 to 15; V6 added no schema changes.

## Current state (as of V6)

- V1–V4: implemented and release-ready.
- V5: implementation complete; production sign-off pending the 30-minute live soak, migration/rollback rehearsal, and feature 02/03 acceptance items.
- V6: implemented; two opt-in changes (staggering, parallel passes) await Release-build re-measurement.
- Next roadmap: none defined yet — V5 sign-off and V6 opt-in re-measurement are the immediate gates.
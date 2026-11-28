# 08 — Technical Architecture & Constraints

> **Status: Draft v0.4 — authored by @tech-leader (co-author).** Supersedes the v0.1 outline.
> **Verified against `develop` @ `2f978eb`, 2026-11-28.** Every claim about this codebase carries a
> `file:line` or a named symbol; every number that was measured rather than read is marked **measured**.
> **Grounding:** `Genelife.Contracts`, `Genelife.Messages`, `Genelife.Application/Services`,
> `Genelife.Domain/Map`, `Genelife.Infrastructure/Persistence`, the V1–V6 roadmaps, and the research
> dossiers `docs/research/world-scale-migration-32000.md` and `building-interiors/`.

This chapter records the **hard technical facts** a feature must respect, so design decisions are
implementable and do not fight the architecture. It is written for two readers: the designer who
needs to know whether an idea is buildable as drawn, and the engineer who needs to know what the
architecture will refuse.

---

## 8.1 The shape of the system

GeneLife is **one server process with a simulation loop inside it**, plus clients that only render
and send intents. There is no separate simulation service and no client-side simulation.

| Layer | Project | Role |
|---|---|---|
| Domain | `Genelife.Domain` | Pure simulation types: components, map generation, economy values, `SimulationRandom`, `SimulationClock`. No I/O, no framework. |
| Application | `Genelife.Application` | The engine: `SimulationEngine`, `SimulationLoop`, systems, services, persistence batches, snapshot construction. |
| Infrastructure | `Genelife.Infrastructure` | PostgreSQL snapshot store, Marten read-model projections, Wolverine, auth. |
| API | `Genelife.API` | HTTP endpoints (`Endpoints/Wolverine/*`), the SignalR `WorldHub`, request handlers. |
| Operator plane | `Genelife.Web` | Blazor admin/observer surface — 11 pages today (Economy, Housing, Family, Lifecycle, Consumption, Social, Players, Appearance, Map, Data, Home). |
| Wire | `Genelife.Contracts` + `Genelife.Messages` | Snapshots, DTOs, intents, commands, events, queries. Both `net10.0`. **Shared with clients.** |
| Client (browser) | `clients/browser` | PixiJS + SignalR prototype slice. Retired as the renderer, kept as a stepping stone. |
| Client (desktop) | `GeneLife.Desktop` (**separate repo**) | Godot 4.7 .NET, C#. Shares `Genelife.Contracts`/`Genelife.Messages`, never the Domain. |

### 8.1.1 The tick loop

`SimulationLoop` (`Genelife.Application/Services/SimulationLoop.cs`) is a `BackgroundService` that runs
on a **dedicated long-running thread**, on a fixed step of **1/20 s = 50 ms** (`FixedDeltaTime`), with
`MaxCatchUpTicks = 5` — beyond five ticks of catch-up the loop **drops** time rather than queueing it.
Every tick, in this order:

1. **Intent drain, before the lock.** `DrainCommands()`, `DrainAdminCommands()`,
   `DrainPlayerIntents()` (`SimulationEngine.cs:602-604`). Player input therefore enters the world
   **only at a tick boundary**, never mid-tick. Admission (auth, rate limit, queue budget, payload
   size) already happened at the edge in `PlayerCommandService`.
2. **Phase 1 — mutate, under the write lock.** A `ReaderWriterLockSlim` write lock (`@lock`,
   `SupportsRecursion`) covers `systems.Update(...)` and the economy event drain. Nothing else may
   mutate the world.
3. **Phase 2 — read, under the read lock.** Event collection and snapshot publication. In the hosted
   path `AsyncSnapshots = true`, so snapshot construction runs on its **own consumer thread** and does
   not count against the tick budget.
4. **Persistence is off the tick thread.** `PrepareSave()` builds an immutable batch under the read
   lock; a save timer writes it. Restore rebuilds every index.

**Consequence for design:** anything a player does is *at least* one tick late, and anything the
client sees is a *published* world, not the live one. A feature that needs sub-tick feedback (a drag,
a hover) must express it as client-side presentation of state the server already sent — never as a
client mutation.

### 8.1.2 Replication is decoupled from the tick

`WorldHub` (SignalR, `[Authorize]`) broadcasts at **`SignalR:TickIntervalMs = 40` ms (25 Hz)** —
deliberately *not* the sim's 20 Hz, so the client interpolates between snapshots instead of jittering.
Per connection:

- **Viewport scoping** — `ViewportRegistry` + `SetViewportRequest`; default radius **32 tiles**, clamped
  to **4..256**. A client only ever receives its area of interest.
- **Rate limits** — snapshot requests 5/s, viewport changes 10/s, player commands 10/s, intent payload
  **≤ 4096 bytes**. These are the client's real budget; a UI that wants to poll must be designed
  against them.

---

## 8.2 The wire contract, and its three replication scopes

`Genelife.Contracts` and `Genelife.Messages` are the **only** shared assemblies. The V3 rule forbids a
client-authoritative simulation, so a client that referenced `Genelife.Domain` would be one refactor
away from a second rule engine. In C# that boundary is enforced by the compiler: the wire format is a
**compile-time fact**, and a DTO change the client has not absorbed is a build error.

There are exactly **three replication scopes**. Every new replicated thing belongs to one of them; a
fourth is a design smell.

### Scope 1 — static world (sent once, cached, versioned)

`MapSnapshot` (per-row RLE, `MapTileRun`) carries the layout: `MapId`, `Version`, `Seed`, `Width`,
`Height`, `MinX/MinY/MaxX/MaxY`, tiles, buildings. `MapDelta` carries `Added`/`Updated`/`Removed`
buildings between versions. The server caches the built snapshot (`cachedMapSnapshot` +
`MapSnapshotCacheKey`). **This is the pattern for anything static and large**: send once, version it,
send deltas after.

### Scope 2 — dynamic world delta (the 25 Hz stream)

`WorldSnapshotDelta` (`Tick`, `SinceTick`, `Version`, `Full`, `Genoses`, `Companies`,
`RemovedGenoses`, `RemovedCompanies`). A full snapshot on connect (`Full = true`, `SinceTick = 0`),
then `Between(from, to)` deltas — computed by **structural equality** (`GenosesEqual`,
`CompaniesEqual`), with explicit removal lists so the client can drop entities without waiting for a
full snapshot.

> **The trap, stated once, loudly.** A field that is not compared in `GenosesEqual`/`CompaniesEqual`
> **never replicates**. The client silently holds a stale value, the server has no error, and the bug
> looks like a rendering fault. A reflective test (`GenosStatusEqualityTests`,
> `CompanyStatusEqualityTests`) fails when a DTO field does not affect equality — **so adding a
> replicated field is a two-file change, always.**

### Scope 3 — scoped request (the map-snapshot pattern, generalised)

For data that is large, static-ish, and only relevant while the player is *looking at one thing*: the
client requests it on entry, caches it until exit, and it never rides the delta stream. Today this is
the map; **§8.5(3) makes interiors the second instance**, and the 32000 world makes it the chunk
protocol.

### The intent channel (client → server)

Typed, polymorphic JSON (`Intent`, discriminator `type`): `move`, `useBuilding`, `playerAction`.
`PlayerActionIntent` carries a **stable byte enum** `PlayerActionKind` (17 kinds,
`PlayerCommandDtos.cs` — *values are persisted and must not be renumbered*), a typed payload from
`PlayerActionPayloads`, a `CommandId` and a `Sequence`. The answer is a `PlayerCommandResult` with
status `Accepted | Rejected | Applied | Superseded` plus a machine-readable `Code` and a human
`Message`.

**The rule this creates — "the UI is the contract":** a dial verb is not a button. Adding a verb means
adding a `PlayerActionKind`, a payload, a server validator, and a catalogue entry (§8.5(1)) — in one
change. The UI cannot show a verb the contract does not carry, which is exactly why the client must
never derive its own action list.

---

## 8.3 Determinism and replay

Determinism is not a nicety here: the whole value proposition is a *simulated* town whose outcomes are
reproducible and auditable.

- **Randomness** — `SimulationRandom` (`Genelife.Domain/SimulationRandom.cs`) derives named streams
  from the world seed (`seed * 397 ^ StableHash(streamName)`, FNV-1a). `Random.Shared` is banned.
  Entity-keyed seeds are derived from stable hashes so genetics does not depend on iteration order.
- **Time** — `SimulationClock` is the single source of month/pay-cycle/year boundaries
  (`TicksPerMonth = 100`, `TicksPerPayCycle = 300`, `TicksPerYear = 1200`). Systems read the clock from
  `SimulationContext`; wall-clock time is never a simulation input.
- **Ordering** — stable iteration order (by `NetworkId`), never dictionary order.
- **Mutation discipline** — *collect → decide → apply*: no entity mutation inside a query callback;
  indices are rebuilt on restore rather than serialised.
- **Map generation is a pure function of the coordinate** (landed as #111): terrain, roads, water and
  zoning are `Classify(tx, ty)` from `(seed, options)`, and the canonical signature is a hash of the
  **spec**, not of the tile array. `MapGenerationGoldenTests` pins today's 256×256 output bit-for-bit.
  This is the correctness gate that makes a 32000×32000 world possible at all (a dense 1.024e9-tile
  array and a 2 GB signature string are the alternative).

**What a new system must do to stay deterministic** — the checklist a reviewer applies:

1. Take tick and clock from `SimulationContext`; never `DateTime.Now`.
2. Take randomness from a **named** `SimulationRandom` stream; never `Random.Shared`, never an
   ambient `new Random()`.
3. Iterate in a stable order; sort by `NetworkId` before any decision that consumes or mutates.
4. Mutate only in the apply phase, never inside a query.
5. Ship a replay test: same seed + same input ⇒ same state, and the test must be able to fail.

Existing replay evidence: the V4 decade replay, the 30-year continuity run, the seed sweep, and the
golden map tests.

---

## 8.4 Persistence and schema discipline

- **Schema version 16** (`PersistedWorldState.CurrentSchemaVersion`,
  `Genelife.Application/Persistence/PersistedWorldState.cs:5`). V1–V5 advanced 4 → 15; V6 added none;
  16 split per-cycle accumulators from the last-cycle display values (`SnapshotMigrations.cs:227`).
- `SnapshotStore` **refuses** a snapshot newer than supported and **migrates** older ones; every bump
  is a migration function plus a fixture, and the V7 persistence track owns the migration/rollback
  rehearsal automation.
- Read models are **Marten projections** (`GenosProjection`, `CompanyProjection`, `MapProjection`,
  `PlayerProjections`, `FamilyProjections`); when an event changes, its projection coverage test
  (`MapProjectionCoverageTests`) must move with it.
- **Rule:** a feature that changes persisted state ships its migration + fixture + projection coverage
  in the same change; a feature that changes a replicated DTO ships the equality update + its
  reflective test in the same change.
- **At 32000** the world persists as a **spec** (seed + options + placements + reservations) and tiles
  regenerate on demand — schema `16 → 17` (world-scale dossier §4.2).

---

## 8.5 The client boundary, concretely — answers to §7.4

Adriano's seven questions are answered here rather than in chapter 07, because each is a contract
decision. Items 1–3 gate the two flagship screens (the intent dial and the dollhouse).

| # | Question (§7.4) | Answer | State |
|---|---|---|---|
| 1 | Action catalogue read model | **Server-provided.** Factored from the existing validator; one rule source. | Decided |
| 2 | Cause lines | **Structured, coded, numeric facts.** Client owns phrasing; no server prose. | Decided |
| 3 | Scoped interior request | **Scope-3 pattern.** No interior field on the building entity. | Decided, not built |
| 4 | Day ribbon planned vs actual | **Committed + scheduled + recorded.** No prediction. | Decided |
| 5 | Fonts and UI scale | **Two bundled fonts; HUD scale independent of camera zoom.** | Requirement (client proves it) |
| 6 | Digest grouping key | **`(eventKind, subjectEntityId)` in a rolling window, server-computed.** | Decided |
| 7 | Presence vs residents | **Already in the contract.** `IsPlayerControlled` is authoritative. | Confirmed |

### (1) The action catalogue is a server read model

The dial must show, per context, the verbs that are valid *now*, with cost, duration, preconditions
and the reason an unavailable verb is unavailable. **The client must not derive this** — deriving it
would make the client a second rule engine, which V3 forbids.

Shape: for `(playerId, genosNetworkId, context)` → entries of
`{ kind, available, refusalCode, costCents, durationTicks, targetHint, asOfTick }`.

The load-bearing detail: the catalogue must be produced by the **same** precondition evaluation the
tick uses. `PlayerActionService` already validates ownership, proximity and domain preconditions and
returns `PlayerActionResult(Applied, Code, Message)`. That evaluation is factored into a pure
`Evaluate(worldState, genos, kind, payload) → (allowed, refusalCode, cost, duration)`, called from
**both** the catalogue read model and the applying path. A parallel list of "allowed actions" written
for the UI would drift from the server on the first balance change — this is the whole reason the
answer is "server-side".

Costs and durations come from domain options, never client constants: offer/invitation lifetimes
(`OfferLifetimeTicks = 100`, `InvitationLifetimeTicks = 100`), travel time from the pathfinder, prices
from the grocery catalog. `asOfTick` lets the client tell a stale catalogue from a fresh one and
re-request on context change (move, enter building, action completes).

Delivery: an HTTP read plus a hub push on context change. No new persistence.

**Delivered (AC-4a).** `PlayerActionService.Evaluate(genos, kind, payload, tick)` is the one
precondition pass; `Apply` calls it and then mutates from the facts it resolved, and
`BuildCatalogue` calls the same method for all 17 kinds. `POST /api/player/actions/catalogue`
returns the envelope (`shapeVersion`, `refusalEnumVersion`), the admin column on `/players` shows the
same entries, and `SnapshotBroadcastService` pushes `SendActionCatalogue` to a player's connections
when an action applies, a move/hire/lease lands. `ActionCatalogueTests` enumerates every kind over
the refusal-state matrix and fails on an 18th kind without fixtures.

### (2) Cause lines are structured facts, not prose

Principle 7.1's "every measure carries a cause line in the simulation's own words" is a **data**
requirement, and the data mostly already exists:

- *"last meal 11 h ago"* — `NutritionState.LastMealTick` exists and is persisted
  (`Components/Consumption/NutritionState.cs:7`; written in `PlayerActionService.cs:623,720`;
  round-tripped at `SimulationEngine.cs:7758,9085`). The cause is `(code = last_meal, sinceTick)`
  rendered by the client against the sim clock.
- Money causes come from `WalletBalance`/`SavingsBalance` and the ledger; housing causes from
  `Tenure`/`HousingTier`/arrears; work causes from `JobPosition`/`Seniority`/`TopSkill`.

The rule: **the server sends facts (code, subject, value, unit, sinceTick); the client owns
phrasing.** Free text from the server is banned — it cannot be localised, and it moves rules into
strings. Where the sim genuinely cannot state a cause, the client shows the raw fact and says nothing
more; an invented explanation is a defect, not a simplification.

### (3) The scoped interior request

**Decision: interiors use the Scope-3 pattern.** `InteriorPlan` (buildingNetworkId, specHash, rooms,
object bindings) is requested on entry, cached until exit, and invalidated by a `specHash`/version
change. **No interior field is added to the building entity on `WorldSnapshotDelta`** — that would
bloat every delta for every building nobody is looking at.

Two things that are *not* the same decision, and must not be conflated:

- The **plan** is scoped, static-ish, cached — Scope 3.
- The **player's location inside** is per-genos dynamic state — Scope 2. It is a replicated field
  (`InteriorLocation`: building id, room id, local tile) and it **must** be added to `GenosesEqual`
  or it will never replicate (§8.2).

This is the same discipline the 32000 world forces on the map, and it is already ruled: chunk = **64×64
tiles** (32000/64 = a clean 500×500 grid), a small **world manifest** (`specHash`, extent, chunk size,
tile-encoding version) fetched once, per-row RLE chunk payloads carrying `specHash` on every response,
and a **server-produced coarse cartogram** (one dominant-terrain flag per chunk → 500×500, ≈250 KB)
because a client cannot derive a world view from 1.02e9 tiles. Interior payloads should be the **same
message family** as the chunk protocol, not a parallel design.

PO rulings in force: interiors are **player-only first, private first**; the web client renders
**generic templates per building kind**, the Godot client renders **procedurally from tilesets** —
two renderers behind one model and one contract.

### (4) The day ribbon: committed, scheduled, recorded — never predicted

The sim has no "plan" entity and must not grow a predictive one. The ribbon draws three honest layers:

- **Committed** — the genos' current action, `TimeRemaining`, and target; all already in `GenosStatus`.
- **Scheduled** — the recurring windows the systems will actually read (venue/office/school/civic open
  ticks; `VenueOptions.TicksPerDay = 24`; `TicksPerPayCycle = 300`), exposed as a read model so the
  client draws the same grid the simulation executes.
- **Recorded** — a bounded history of action intervals for the current day.

The client may render *committed* and *recorded* differently; it may not infer a future outcome. "You
will be fired" is not a state the server has, so it is not a thing the ribbon can say.

### (5) Fonts and UI scale in the client

Requirement: the client **bundles two fonts** — a serif for the record and a mono with **tabular
figures** for every quantity — as project resources, and **UI scale (100/125/150 %) is a property of
the HUD layer, independent of the world camera's zoom**. The world zoom must never re-lay-out the
interface. Godot's default font stack ships no serif, so this is an asset requirement, not a styling
preference. The exact mechanism (a scaled Control root / scaled Theme font sizes on the HUD
`CanvasLayer`, OpenType `tnum` for the mono) is the client's implementation detail and is proven by
screenshot **in the client repo**, not asserted here.

### (6) The digest grouping key

Group by **`(eventKind, subjectEntityId)`** within a rolling window (an in-game day, or a session),
counting occurrences and keeping first/last tick. The key is **server-computed**, because a grouping
rule is a rule. Source: the existing outbound event channel and the domain events in
`Genelife.Messages/Events`, filtered to entities the player is connected to (own genos, household,
workplace, family, venue). Ambient per-tick events (`GenosNeedsChanged`) are suppressed — the digest
carries state *changes* the player did not initiate. Implementation is a per-player projection in the
API: **no new persistence, no schema change.**

### (7) Presence and residents — already in the contract

Confirmed, both halves exist:

- `GenosStatus.IsPlayerControlled` is a replicated field on **every** genos, and it **is** compared in
  `GenosesEqual` — so "is a player" arrives as data. It must be read from there and **never inferred**
  client-side from a name, a connection, or a client-side list.
- A resident's display name is `GenosStatus.Name` (already replicated).
- A player additionally has `PlayerPresenceDto` (`PlayerId`, `DisplayName`, `GenosNetworkId`, `State`,
  `LastSeenTick`) and the nearby list `PlayerDirectoryDto` (`Scope`, `Radius`, `Items`).

Privacy is separate: `SocialPrivacyDto.ShowDisplayName` governs a **player's** display name only; the
resident name and the "is a player" chip remain truthful regardless.

---

## 8.6 The observability rule (admin-first)

**Every feature ships its read model, its endpoint and its admin surface alongside it.** Today that is
18 endpoint families under `Genelife.API/Endpoints/Wolverine/` and 11 operator pages under
`Genelife.Web/Pages/`. A feature whose state cannot be inspected from the operator plane is not
finished, because the only other way to debug it is a replay.

The corollary matters for chapter 07: **global metrics are not play-surface data.** Gini, vacancy,
tick time, allocation and population belong to the operator plane; the play surface shows the player's
life. The two surfaces are different readers of the same world, and neither substitutes for the other.

---

## 8.7 The performance envelope (context, not a workstream)

Recorded so features do not break it — **not** as a backlog. Standing PO ruling: balance and
performance work is not a current priority, and this chapter opens none.

- **20 Hz, 50 ms budget, 2,000 genoses, 32 players.** V6 left roughly **5× headroom**: the 2k gate
  runs **p95 ≈ 1.97 ms measured** against 50 ms, allocation flat.
- **p95 is the accepted gate; p99 is harness diagnostic and deliberately unbudgeted** (the tail varies
  more between runs than p95 does, so a p99 threshold fails spuriously).
- **The binding constraint is allocation, not p95** — per-tick allocation sits near its budget while
  p95 sits far under its own, and the top allocators are the economy systems. A feature that adds
  per-genos per-tick allocation is spending the real budget even when the tick time looks fine.
- The 32000 streaming/chunking work is **feasibility and correctness, not optimisation**: without it
  the world cannot be generated at all; with it the per-tick dynamic cost stays viewport-bounded.

---

## 8.8 Constraints a feature must respect — the checklist

The list a design must survive. Each is a decision already made; none is a preference.

1. **No client authority.** Clients render and send intents. No client references
   `Genelife.Domain`/`Genelife.Application`.
2. **Intent-only mutation, drained at tick start.** No mutation from a request thread, ever.
3. **Determinism** — named `SimulationRandom` streams, `SimulationClock` for time, stable ordering,
   collect→decide→apply, replayable.
4. **New replicated field ⇒ equality update + reflective test.** Otherwise it never replicates.
5. **Money is integer cents and every transfer is ledgered.** No float money, no unledgered transfer.
6. **No feature flags.** `DocsConsistencyTests.No_feature_flag_or_conditional_architecture_path_remains`
   fails the build if `FeatureFlags` reappears in `appsettings.json` or `Program.cs`; dual-path
   transitions are expressed as **config + protocol versioning**.
7. **Chunk size (64) and the snapshot message shape are contract constants**, decided once before the
   protocol work — the only genuinely irreversible choice in the world-scale migration.
8. **A UI verb is a contract verb** (the dial rule, §8.2).
9. **Cause lines are structured facts**, never server prose (§8.5(2)).
10. **Read model + endpoint + admin surface ship with the feature** (§8.6).

---

## 8.9 The desktop client (decided, in progress)

- **Godot 4.7 .NET, C#, desktop-only.** Not GDScript (thin tooling glue excepted). C# cannot export to
  web in Godot 4, so the browser target stays PixiJS and there is no wasm export job.
- **Target framework `net10.0`**, matching the sim's projects so `Genelife.Contracts`/`Genelife.Messages`
  are referenceable. The `net10.0`-inside-Godot spike **passed** on this host, so the `net8.0` fallback
  is not needed.
- **Its own repository** (`github.com/takethispie/GeneLife.Desktop`), its own branch flow (PRs to its
  own `develop`; `main` is the PO's line), its own CI: a cheap `dotnet build` compile job (works with no
  engine installed) **plus** a headless import/boot job, because a compile-only gate is green while the
  game is broken. **Any job that runs the project must build Debug** — a Release-only build cannot
  resolve the C# assembly at runtime.
- The client's job in this GDD: render the sim's world **from the contract**, own the "Civic Paper" HUD
  (chapter 07), and stop hardcoding anything the manifest carries (world extent above all).

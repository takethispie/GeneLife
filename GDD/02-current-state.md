# 02 — Current State & The Bridge

> This chapter is the honest picture of where GeneLife is today, and what "the bridge
> between the web game client and the desktop client" concretely means. It is grounded in
> the shipped V1–V6 roadmaps and the team's shared memory. It exists so that every later
> chapter is anchored to reality rather than a wish.

## 2.1 What is already built (V1–V6)

| Roadmap | Theme | State |
|---|---|---|
| V1 | Realistic population simulation — economy, housing, life stages | Implemented |
| V2 | Spatial world, buildings, player presence | Implemented |
| V3 | Browser game client (PixiJS + SignalR) | Implemented vertical slice |
| V4 | Relationships, attraction, reproduction, inheritance | Release-ready |
| V5 | Playable society — player life, appearance, consumption, social, live ops | Released |
| V6 | Tick performance | Implemented (both opt-ins closed) |

**In one line:** the *simulation* is mature and the *society* is real; what is immature is
the **client** that renders it and the **personal-control UX** on top of it.

### 2.1.1 The simulation (V1, V4, V5) — the strong half
- **Economy:** integer-cent money with every transfer ledgered; companies, job postings,
  applications, payroll, revenue, tax, pensions, credit, insolvency, savings.
- **Housing:** households, rent, utilities, eviction, construction, ownership, homelessness,
  overcrowding.
- **Life stages:** education → work → retirement → elderly → death; schools and skills,
  careers, unemployment, health/mortality, estates, immigration.
- **Family (V4):** traits and genetics (a `Traits` record struct, byte-backed enums),
  directed relationship edges (`Parent`/`Child`/`Partner`/`ExPartner`/`Guardian`/`Ward`),
  integer basis-point attraction, pairing, pregnancy/birth, childhood gates, widowhood,
  guardianship, relationship-based inheritance with minor trusts and housing transfer, births
  as a continuity path supplementing immigration.
- **Player life & society (V5):** player agency via typed intents, appearance derived from
  traits/age/sex, utility networks (water/electricity) with outages and household billing, a
  grocery catalog with stock/pricing/nutrition, text chat, presence, friendships, blocks and
  reports, consensual player partnerships, venues and moderation.

### 2.1.2 The world (V2)
- Integer tile coordinates **centred on the origin** (default **256×256**), `TilePosition`
  authoritative with `Location` derived; deterministic generation, occupancy/placement,
  building bootstrap, admin placement, persistence/restore, map read API, an admin map
  console, player spawn/commute presence, movement/pathfinding and area-of-interest
  viewport filtering.

### 2.1.3 The client (V3, V5) — the half being replaced
- `clients/browser`: TypeScript + Vite + **PixiJS** + SignalR + a DOM HUD. Renderer, camera,
  RLE map decoding, a replication store interpolating at 25 Hz, viewport sync, input/intents,
  notifications, settings and a debug overlay.

## 2.2 The cross-roadmap invariants (binding on every future feature)

These hold across all roadmaps and every chapter of this GDD must respect them:

1. **Server authority everywhere.** The client never mutates the world; player actions are
   typed intents/commands drained at tick start under the write lock.
2. **Determinism.** `Random.Shared` is banned; named `SimulationRandom` streams; stable
   iteration order; replay tests at every stage.
3. **Money conservation.** Integer-cent `Money`; every transfer is ledgered; balance bands
   (rent burden, vacancy, Gini, company failures) are reported at 1k/2k.
4. **Collect → decide → apply.** No entity mutation inside query callbacks; indices are
   rebuilt on restore.
5. **Admin-first observability.** Every feature ships read models, endpoints and admin
   surfaces alongside it.
6. **Schema discipline.** Persistence reached **schema 16** (`PersistedWorldState.CurrentSchemaVersion`);
   V1–V5 advanced 4 → 15 and a later change split per-cycle accumulators from last-cycle
   display values (→ 16). V6 added no schema changes. Every bump is a migration function plus a
   fixture; the desktop-client work adds none.

## 2.3 What "the bridge" means

The bridge is a **client transition**, not a reboot:

- **From:** a PixiJS **browser** client that is a *prototype-only slice* — its purpose was to
  validate the model and the UX, and its sprite code is expected to be **retired**.
- **To:** a **desktop client in Godot, written in C#**, which reuses the C# simulation
  tooling and shares the **wire contract** (`Genelife.Contracts` / `Genelife.Messages`) — but
  **never** the domain/application assemblies, because V3 forbids a client-authoritative
  simulation and referencing the sim would invite a second one.
- **Unchanged by the transition:** the **model**, the **contract**, and this **GDD**. These
  are the durable artifacts. Only the renderer is thrown away.

### 2.3.1 What carries over vs what is retired

| Carries over (durable) | Retired / rebuilt (prototype) |
|---|---|
| All simulation systems (V1–V5) | PixiJS renderer (tiles, sprites, layers) |
| Wire contract & intents | DOM HUD panels |
| Persistence & schema | Browser camera / visual test fixtures |
| The UX *decisions* (what the player can do) | The exact browser layouts and styling |

**Consequence for this GDD:** the UI/UX chapter (07) specifies the **design intent and screen
inventory**, and mockups are expected to be **new designs that serve the desktop client**, not
copies of the current web layouts. This is the product owner's explicit instruction.

## 2.4 Scale and performance reality (as of V6)

- The simulation loop targets **2,000 genoses** inside its tick budget under 32-player load;
  V6 left roughly **5× headroom** at the p95 tick (2k gate p95 ≈ 2 ms against a 50 ms budget).
- The API's population ceiling (≈5,000 genoses ⇒ ≈411 buildings) already **exceeds the default
  256×256 map's lot count** (400 lots) — a real constraint that the world-size work addresses.
- Balancing and performance work is **out of scope at the current stage** by standing product
  ruling: this GDD records the numbers as *context*, and does not open new balance/perf work.

## 2.5 The gap the bridge must close

| Area | Today | Target |
|---|---|---|
| Renderer | PixiJS prototype (browser) | Godot C# desktop renderer |
| Control feel | Functional but prototype-grade | Sims-grade legible personal control |
| World size | 256×256 default | Larger open world (sizing + scale work) |
| Interiors | None (walk to a door, nowhere to go) | Player-only cutaway interiors first |
| UI | Web-client layouts | New desktop-first design language |
| Identity | Implicit in the roadmaps | **This GDD** — explicit and normative |

# Growing the world to 32000×32000 — migration surface, streaming discipline, and phased sequencing

**Author:** @roberto (Solution Researcher) · **Date:** 2026-11-28 · **Branch:** `docs/roberto/world-scale-32000-migration`
**Verified against:** `develop` @ `dc3dc6e` · **Card:** *"research(world): settle the sim-vs-client world size disagreement (256×256 vs 32000×32000)"* (`t_2906f2c1`)
**Ruling in force:** PO, 2026-11-28 — *the client's city map **is** the simulation's world, and **32000×32000 is the intended size**; the sim's `MapOptions` is the number that changes.*

**Companion documents.** This file is the *consequence* of the ruling and is written to be read **after**
[`building-sizing-and-world-scale.md`](building-sizing-and-world-scale.md) (PR #105) — that document derives the
*force* that pushes the world bigger (interior footprint → lot pitch → map extent) and the N/N² law; this one does not
repeat that arithmetic. It also assumes [`terrain-manifest.md`](terrain-manifest.md) (PR #109) and
[`godot-client-decision.md`](godot-client-decision.md) (PRs #106/#107).

**Scope line.** Docs-only. No `.cs` file, no client file, no map is changed. Every claim about *this* codebase carries
a `file:line`. Every measured number is marked **measured** and reproducible from the repository. Where a number cannot
be established from the repository, the text says so instead of inventing one. Nothing here is in the balance or
performance area — the streaming work below is **architecture required for feasibility/correctness**, not a tick-budget
optimisation (see §5.4).

---

## 0. What the ruling settles — and what it does not

The card was raised as a *disagreement to adjudicate* (client says 32000, sim says 256). The PO has now **ruled**:

| Question the card asked | Status |
|---|---|
| Which number is correct? | **Settled — 32000×32000 (tiles).** The sim's `Width/Height = 256` is the value that changes. |
| Is the client's city map the sim world or an art backdrop? | **Settled — it IS the sim world.** It must therefore be rendered from the snapshot, per the V3 rule that forbids a client-authoritative simulation (`docs/plans/roadmap-v3/README.md`). |
| Where did 32000 come from? | **Answered — the PO's own instruction** ("much bigger than 256×256, more like 32000 by 32000", quoted verbatim in the client repo's working history). It was not an invented constant. |

This document therefore **does not** re-open the number. It answers the three consequences the ruling creates:
**(1)** the full migration surface for growing the world to 32000×32000 (§1–§3), **(2)** the streaming/chunking
discipline both sides need at 1.02e9 tiles (§4), and **(3)** the cost and a safe phased sequence that keeps the sim
working throughout (§5).

### 0.1 Two provenance facts worth recording, because they change the next card

1. **The 32000 constant lives in a *separate repository*.** The client is `G:\dev\GeneLife.Desktop`
   (`github.com/takethispie/GeneLife.Desktop`, branch `main`), not the main solution. Its city map is
   `scripts/City/CityGen.cs`, and the constant is `CityGen.Size = 32000` (`scripts/City/CityGen.cs:49`), committed as
   *"feat(godot-dev): procedural chunk-streamed 32000x32000 city map"* (`fda8019`/`1221414`). It is a **procedural,
   chunk-streamed generator**, not a painted tilemap — see §4.3.
2. **The client's own documentation currently says the opposite of the ruling**, and the ruling supersedes it. The
   client's `references/city-map.md` (its internal skill note) states: *"A client-side 'city map' is a
   visual/standalone artefact, not the sim world — say so explicitly, do not imply the sim grew."* Under the
   2026-11-28 ruling this is now **wrong and must be corrected** as part of Phase 1 (§5): the city map is the sim
   world, and the client must read its extent from the contract rather than hardcoding `CityGen.Size`. This is a
   doc-drift item the migration must carry, not a research question — flagging it here so the client card inherits it.

> **Interpretation recorded for the migration.** The PO's phrasing ("32000 by 32000") was ambiguous between *tiles*
> and *world units*; the client resolved it as **tiles** (`CityGen.Size` in tiles, `TilePx = 32`,
> `scripts/City/CityGen.cs:48-64`), and `MapOptions` is also in tiles (`TileSize = 1f` ⇒ 1 tile = 1 world unit,
> `Genelife.Domain/Map/MapOptions.cs:17`). The two units coincide, so **32000 tiles = 32000 world units = 32000
> `MapOptions.Width`**. If the PO meant something else, say so before Phase 1 — changing the unit later re-does every
> number below.

---

## 1. The scale, in the only units that matter: bytes and blocks

32000×32000 = **1.024e9 tiles**. That single fact invalidates every *dense, whole-map* structure in the codebase,
because each one is allocated as `O(area)`:

| Dense structure | Code | Today (256² = 65,536) | At 32000² (1.024e9) |
|---|---|---|---|
| Terrain flag array | `MapGenerator.cs:25` `new TileFlags[Math.Max(1, bounds.Area)]` (`TileFlags : byte`, `TileFlags.cs:4`) | 65 KB | **~1.0 GB** |
| Signature builder | `MapGenerator.cs:212-213` appends **every tile** to a `StringBuilder`, then SHA-256s it | ~65 K chars | **~1.0e9 chars ≈ 2 GB** (string + UTF-8 copy) |
| Water carve loop | `MapGenerator.cs:57-76` `waterTiles = bounds.Area × WaterFraction` (0.02) with `waterTiles × 20` attempts | ~1.3 K tiles | **~2.0e7 placements, ~4.1e8 attempts** |
| Occupancy tile array (clone) | `WorldMap.cs:17,45` `TileFlags[] tiles = (TileFlags[])layout.Tiles.Clone()` | 65 KB | **~1.0 GB** |
| `ReservedTileCount` / `Rebuild` | `WorldMap.cs:32-38, 423-424` linear scans of the whole array | 65 K | **1.0e9 iterations** |
| Generated lots list | `MapGenerator.cs:107-141` + `WorldMap.cs:16`; lots ≈ `(32000/12)²` at default `RoadSpacing = 12` | 400 lots | **~7.1e6 lot objects** (hundreds of MB) |
| Per-row RLE snapshot | `SimulationEngine.cs:3105-3141`; **measured** 256² = 7,661 runs / 726,789 B for `map-snapshot-large.json` | 7,661 runs, 727 KB | **~1.2e8 runs, ~11 GB** if scaled ×15,625 |
| Client RLE decode buffer | `clients/browser/src/simulation/map/rle-decoder.ts:27` `flags: Uint8Array` sized `width×height` | 65 KB | **~1.0 GB in the browser tab** |

**Read the last two rows together.** The snapshot protocol (`MapSnapshot` with per-row RLE, `Genelife.Contracts/Dtos/MapSnapshot.cs`) and the client decoder (`rle-decoder.ts`) are the *same coin*: the server builds a whole-map payload, the browser decodes it into a whole-map byte array. Both are `O(area)`. At 32000² they are impossible — not slow, **impossible** (~11 GB payload, ~1 GB decode buffer). The Godot client already sidesteps this by never materialising the world (`CityGen.Classify` is a pure function; only visible 64-tile chunks exist, `scripts/City/CityGen.cs:153-210`, `references/city-map.md`). **That is the discipline both the server and the browser client must adopt (§4).**

---

## 2. The UX consequence the RLE number hides: a whole-map view is meaningless at 32000

The browser camera renders **32 px/tile at its default zoom**, is clamped to **`minZoom = 12`, `maxZoom = 128`**
(`clients/browser/src/render/camera.ts:19,29-30`), and frames the whole map with `fitZoom()`, which is itself clamped
to `Math.min(32, …)` (`camera.ts:174-179`).

At 256 tiles a whole-map fit needs ≈1600/256 ≈ **6.25 px/tile** — already below `minZoom`, which is why the existing
`fitZoom` clamp exists. At 32000 tiles a whole-map fit needs ≈1600/32000 ≈ **0.05 px/tile** — **240× smaller than
the current minimum**. So:

- You **cannot** frame a 32000-tile world with a continuous tile render; per-tile detail vanishes long before the
  whole city is on screen. The client's own city-map note reached the same conclusion ("a 1-tile road cannot survive
  downsampling") and answers it with a **block cartogram** (1 px per city block) and a separate 1:1 sample
  (`references/city-map.md`).
- Therefore the browser client's "overview" can no longer be the map render itself. It becomes either a
  **cartogram/summary layer** the server produces, or a **zoomed viewport that streams chunks** with the overview as a
  minimap. Either way it is a **client redesign**, not a constant bump.

This is the second place where "grow the world" is not a scaling problem but a **category change**: the continuous
tile renderer stops being a whole-world view at all.

---

## 3. The migration surface — everything that assumes 256 or density

Grouped by where the change lands. Each row names the artefact, why it assumes the current world, and the shape of the
change (no line-level design here — that is the implementation cards' job).

### 3.1 Domain map model (the generator and the index)

| Artefact | Assumes today | Why it breaks at 32000² | Change shape |
|---|---|---|---|
| `Genelife.Domain/Map/MapOptions.cs:15-17` | `Width=Height=256`, `TileSize=1` | It is *the* number changing | Set to 32000; keep it config-owned, never in game logic (rule already stated at `docs/plans/roadmap-v3/README.md:128`) |
| `MapOptions.Bounds` (`:20-23`) | origin-centred ±128 | Still correct, just ±16000 | none (formula unchanged) |
| `MapGenerator.Generate` (`:21-44`) | allocates + fills a **dense** `TileFlags[area]` | 1 GB array; unusable | **Rewrite to pure-function + chunk** (§4.1) |
| `MapGenerator.CarveWater/CarveRoads` (`:52-105`) | iterate/repeat over `area` | 1e9 iterations | fold into `Classify(tile)` |
| `MapGenerator.BuildLots` (`:107-141`) | materialises **every** lot in a `List<MapLot>` | ~7.1e6 objects | make lots **derived on demand** per chunk |
| `MapGenerator.ComputeSignature` (`:199-227`) | SHA-256 over a string of every tile | 2 GB string | hash the **generator spec** (seed + options), not the tiles |
| `WorldMap.tiles` (`:17,45`) | dense clone of the map | 1 GB | sparse: terrain via `Classify`, occupancy via `Dictionary<TileCoord,…>` (already the case for occupancy, `:15`) |
| `WorldMap.ReservedTileCount`/`Rebuild` (`:32-38,423-424`) | linear scans of the dense array | 1e9 iterations | scan **placements/lots**, not tiles |
| `Genelife.Domain/Map/MapGenerationOptions.cs:7` | `RoadSpacing = 12` | density knob, interacts with the sizing doc | decided with PR #105's lot-pitch decision, not here |

### 3.2 Persistence and snapshot contract

| Artefact | Assumes today | Why it breaks | Change shape |
|---|---|---|---|
| `Genelife.Contracts/Dtos/MapSnapshot.cs` | full-map, per-row RLE tiles sent once | ~11 GB at 32000² | **viewport/chunk-scoped snapshot** protocol (§4.2) |
| `SimulationEngine.BuildMapTileRuns` (`:3105-3141`) | walks every row of the whole map | 32000 rows × runs | build runs **per requested viewport/chunk only** |
| `PersistedWorldState` schema v16 (`Genelife.Application/Persistence/PersistedWorldState.cs:5`) | persists a whole `PersistedMap` (`:641-653`) incl. `Tiles` | whole-map tile payload on disk | bump schema; persist the **generator spec**, not the tiles; keep placements |
| `PersistedPlacement` (`:671-682`) | per-building `X,Y,W,H,Entry…` | fine — buildings are sparse and bounded | none |
| `MapSnapshotCacheKey` (`SimulationEngine.cs:3143-3150`) | keys on width/height/version | still fine, but must key per-viewport too | extend key with the requested chunk |
| Coordinate encoding | `MapTileRun.X/Y`, `TileCoord(int X, int Y)` (`Genelife.Domain/Map/TileCoord.cs:7`), snapshot `MinX/MaxX` are **`int`** | **`int32` is fine** — but note `±16000` is *just* inside `int16`'s `32767`, so any future `short`-packing optimisation is off the table (0 margin), and float32 world coords (±16000 units, and ±1.02e6 px at 32 px/tile) are exact (float32 is exact to 2²⁴ ≈ 1.67e7) | keep `int`/`float`; **document** the encodings |

### 3.3 Pathing, AOI, and movement

| Artefact | Assumes today | Why it breaks | Change shape |
|---|---|---|---|
| `Genelife.Domain/Map/SpatialIndex.cs:45-46` | `CreateDefault()` = `new(cellSize:10f, bounds:new WorldBounds(halfExtent:1000f))` | **the AOI world is hard-capped at ±1000 units (2000 tiles)**, and `Cell()` **clamps** positions to those bounds (`:48-53`) — at 32000 the index silently mis-buckets everything beyond ±1000 | AOI bounds must be **derived from `MapOptions`** (or made per-chunk); the constant is a second hidden world-size constant |
| `Genelife.Domain/Map/TilePathfinder.cs` | A* over the whole `WorldMap`, `maxLength` **4096**, Manhattan heuristic (`:17,74`) | a 32000-tile world makes cross-city paths exceed 4096 trivially; the priority queue and `cameFrom`/`gScore` dictionaries grow to the search frontier | **hierarchical/chunked pathing** (chunk graph + local A*), or keep 4096 but define movement as **local** (commute within a chunk) — a design decision (§5.5) |
| `MovementIndex` (`:14-79`) | per-entity route queues | fine (per-entity, not per-tile) | none |
| AOI/culling | per-viewport (`AoiFilter`, `building-sizing-and-world-scale.md:258-259`) | stays bounded **only if** `SpatialIndex` bounds are fixed | ties to the row above |

### 3.4 API / admin surface

| Artefact | Assumes today | Why it breaks | Change shape |
|---|---|---|---|
| `Genelife.API/Endpoints/Wolverine/MapEndpoints.cs:25-27` | hard-caps dimensions at **64–512** per side, population ≤ 5000 | **rejects 32000 outright** (`BadRequest`) | raise/remove the cap; replace the per-side bound with a **tile-budget** (e.g. `width×height ≤ N`) so a 32000² request is legal but bounded |
| `Genelife.Web/Pages/Map.razor:429-432` | defaults `createWidth/Height = 256`, population 500 | wrong default | default to the PO's 32000 (or leave 256 as the *editable* default until Phase 4) |
| `CreateMapRequest` / `MapCreation.cs` | carries width/height; broadcasts `WorldResetMessage` | fine structurally | validate against the new budget |

### 3.5 Tests and fixtures (the numbers the migration must re-pin)

These **hardcode a map size** and will fail or need new values (traced in PR #105 §3.2; re-listed with the resize
context):

- `Genelife.Domain.Tests/Map/MapFoundationTests.cs:20,27,40-41` — 64×64, `RoadSpacing = 10`; asserts a 12-tile footprint.
- `Genelife.Domain.Tests/Map/BuildingRelocationTests.cs:21-22` — 64×64.
- `Genelife.Domain.Tests/Map/MovementTests.cs:17-18,45-46` — 64×64.
- `Genelife.Domain.Tests/Services/MapCreationTests.cs:20,37-38` — asserts **128×128**.
- `Genelife.Api.IntegrationTests/Endpoints/Wolverine/MapCreationEndpointTests.cs:44,81,87,128-129` — asserts 128×128
  and **the 64–512 rejection** (`:81` a width of 32, `:87` population 9000) — these move with the cap change.
- `Genelife.Api.IntegrationTests/Marten/ProjectionIntegrationTests.cs:92,116,155,161,170` — hardcodes `MapGenerated`
  width/height (64/64, 128/96).
- `Genelife.Domain.Tests/Map/MapSnapshotTests.cs:38` — placement/building count parity.
- `Genelife.Domain.Tests/Marten/MapProjectionCoverageTests.cs:26-33` — every durable map event needs a projection;
  **a schema/event change from the resize must land with a projection**.
- `Genelife.Web.Tests/Pages/MapTests.cs:11-33` — stubs a 32×32 map status.
- Client fixtures: `clients/browser/src/test/fixtures/map-snapshot-large.json` (**256², 7,661 runs, 726,789 B**,
  **measured**) and `map-snapshot.json` (32×32); `map-delta.json` same shape.
- Client visual baselines (pixel-exact): `clients/browser/e2e/visual/appearance.spec.ts-snapshots/*-world-visual-*.png`
  and `stress-density.spec.ts-snapshots/*.png`.

**The tests do the migration a favour:** they pin the *current* numbers explicitly, so the resize is a visible,
reviewable diff rather than a silent behaviour change — provided the resize card updates them in the same commit (the
PR #105 "footprint + lot pitch are one atomic change" rule extends here: **size + fixtures + baselines + schema are one
atomic change**).

### 3.6 Client surface

| Artefact | Assumes today | Why it breaks | Change shape |
|---|---|---|---|
| `clients/browser/src/simulation/map/rle-decoder.ts:27` | decodes the whole snapshot into a dense `Uint8Array` | ~1 GB buffer | decode **per viewport chunk**; keep a chunk cache |
| `clients/browser/src/render/camera.ts:29-30,174-179` | `minZoom 12`, `fitZoom` clamp 32 | whole-map fit needs 0.05 px/tile | reworked zoom model + minimap/cartogram (§2) |
| Browser AOI/viewport client | requests `TileRect` per viewport (`viewport-client.ts`) | already viewport-shaped — good | the **server** must answer per viewport (§4.2) |
| **Godot** `G:\dev\GeneLife.Desktop` `scripts/City/CityGen.cs:49` | `Size = 32000` hardcoded; pure-function + chunk-streamed | already the right architecture | stop hardcoding: **read extent from the contract**; render the **sim snapshot**, not its own generator (V3) |
| Client docs | `references/city-map.md` says "not the sim world" | contradicts the ruling | **correct it** in Phase 1 (§0.1) |

### 3.7 Documentation that carries the number (must not be left stale)

- `docs/plans/ROADMAPS.md:37` ("256×256 default").
- `docs/plans/roadmap-v3/README.md:125,128,184` — the RLE format, the **explicit rule** *"Do not hardcode 256×256 or
  32 px in game logic"* (the convention this migration must honour), and the "256×256 map fixture" line.
- `docs/research/building-interiors/01-problem-and-current-state.md:28` and `05-contract-and-server.md:75` — both
  state "256×256 = 65,536 tiles".
- The docs guard `Genelife.Domain.Tests/Docs/DocsConsistencyTests.cs` (`Category=Documentation`) asserts **nothing**
  about map size, so it stays green through the resize — and it **forbids reintroducing `FeatureFlags`** into
  `appsettings.json`/`Program.cs` (`:32-38`). **That last point is load-bearing for the sequencing** (§5.3).

---

## 4. The streaming/chunking discipline both sides need

At 1.02e9 tiles nothing materialises the world. The Godot city map is the working reference implementation of the
discipline (`CityGen.Classify` pure function; `CityChunkManager` keeps ~50 visible 64-tile chunks; `references/city-map.md`).
The server and the browser client must adopt the **same two rules**.

### 4.1 Rule 1 — generation is a pure function of the coordinate, not a stored grid

- **Terrain, roads, water, zoning** are `Classify(tx, ty) → flags` — deterministic from `(MapOptions.Seed, options)`,
  allocation-free, with a stable integer `Hash(x, y)` for per-tile variety (the exact pattern in
  `scripts/City/CityGen.cs:84-210`).
- **Consequences in this codebase:** `MapGenerator` stops returning a dense `GeneratedMap.Tiles`; the canonical
  signature becomes a hash of the **spec** (`seed + options`), not the tile array (`MapGenerator.cs:199-227`); water
  is a classifier predicate, not a random fill loop (`:52-76`).
- **Occupancy and lots stay sparse** (they already are dictionaries for occupancy, `WorldMap.cs:15`): a lot is
  *derivable* from `(tile, RoadSpacing)` on demand, so `BuildLots` no longer allocates 7.1e6 objects.
- **Why it is free:** a chunk is generated only when asked for, and discarded when off-screen — the per-tick dynamic
  cost does not scale with the world (the argument already made at `building-sizing-and-world-scale.md:258-259`).

### 4.2 Rule 2 — the snapshot is viewport/chunk-scoped, not whole-map

- Today `MapSnapshot.Tiles` is the **whole map** in per-row RLE (`MapSnapshot.cs`; `SimulationEngine.cs:3105-3141`).
  This is the single biggest protocol change. It becomes **per-request viewport chunks**: the client asks for the
  chunks it can see (it already knows its `TileRect`, `viewport-client.ts`), the server answers those runs only.
- **`SimulationEngine.BuildMapTileRuns` becomes `BuildMapTileRuns(chunkRect)`** — the RLE algorithm is unchanged, its
  input domain shrinks from 1e9 to ~(viewport tiles).
- **Versioning:** an additive, versioned chunk message (or a `MapSnapshot` variant with a `TileRect` field) keeps old
  clients loading old worlds during the transition. The existing `MapSnapshotCacheKey` (`:3143-3150`) extends with the
  requested rect.
- **Persistence:** the *world* persists as a spec (seed + options + placements + reservations), the *tiles* are
  regenerated on demand. Schema bump `16 → 17` (`PersistedWorldState.cs:5`) with a Marten projection for the event
  change (`MapProjectionCoverageTests.cs:26-33`).

### 4.3 Rule 3 — the client keeps a chunk cache, never a whole-map array

- The browser's `rle-decoder.ts` must decode **into a bounded chunk cache** (LRU of visible ± margin chunks), exactly
  as `CityChunkManager` keeps ~50 chunks live (`references/city-map.md`). A `Uint8Array` of `width×height` is deleted.
- Rendering drops the "one big world container with a whole-map texture" model; it draws the chunk cache. The
  minimap/cartogram (§2) is a **separate, server-produced summary** (or a coarse classifier the client runs itself —
  the Godot client does the latter, `CityImages.cs`).

### 4.4 The one number that must be a *decision*, not a constant

Chunk size (Godot uses 64 tiles) and the snapshot's chunk granularity must be **one shared constant in the contract**,
not two independent literals. If the server ships 64-tile chunks and the client caches 32-tile chunks, every load is a
re-slice. **Propose 64 tiles, owned by `Genelife.Contracts`.**

---

## 5. Cost and sequencing — keeping the sim working throughout

### 5.1 The shape of "safe"

The sim must never be left unable to generate a world. Because the size is **configuration** (`MapOptions`), the safe
pattern is: **land the chunked architecture while the default is still 256 and every existing test is green, then flip
the default and regen.** No phase may require the next phase to have landed to keep the build/tests passing.

### 5.2 The phases

| Phase | Deliverable | Default live size | Keeps sim working by | Rough size |
|---|---|---|---|---|
| **0 — Record the ruling** | This doc + corrected client `references/city-map.md`; the number written into `MapOptions` comments | 256 | Docs only | this PR |
| **1 — Pure-function generator (server)** | `MapGenerator` → classifier behind the current API; `GeneratedMap.Tiles` becomes lazy; signature = spec hash. Existing tests keep passing (they compare **behaviour**, not the array) | 256 | The classifier reproduces today's 256 output bit-for-bit; a golden test pins it | **L** |
| **2 — Sparse index + AOI** | `WorldMap` occupancy/lots sparse; `SpatialIndex` bounds derived from `MapOptions` (kills the `1000f` cap, `SpatialIndex.cs:46`) | 256 | Behaviour-identical at 256; a test proves the AOI cap is gone | **M** |
| **3 — Viewport snapshot protocol** | `MapSnapshot` chunk variant + `BuildMapTileRuns(rect)` + `rle-decoder` chunk cache + browser viewport requests | 256 (one chunk = whole small map) | Old whole-map path retained/versioned until both clients ship | **L** |
| **4 — Flip the default + persist spec** | `MapOptions.Width/Height = 32000`; `MapEndpoints` cap → tile-budget; `Map.razor` default; schema `16→17` + projection; **regen fixtures + visual baselines**; update the §3.7 docs | **32000** | Phase 1–3 already make 32000 *feasible*; this only changes the number | **M + regen** |
| **5 — Client polish** | Browser minimap/cartogram + zoom model (§2); Godot client reads extent from the contract and renders the snapshot | 32000 | Opt-in on 256 until done | **L** |
| **6 — Pathing decision lands** | Hierarchical/chunk pathing **or** local-movement rule (§3.3) | 32000 | 4096 budget already caps work; correctness bounded until then | **M–L** |

**Critical ordering constraint:** Phase 1 must precede Phase 4. Flipping the default before the generator is
pure-function makes generation **throw or OOM** (the dense array is 1 GB; the signature string is 2 GB). Phase 1
before Phase 4 is therefore not a preference, it is the correctness gate — the same "footprint + lot pitch are one
atomic change" logic from `building-sizing-and-world-scale.md:388-389`.

### 5.3 The flag rule (why "just feature-flag it" is not available)

`DocsConsistencyTests.No_feature_flag_or_conditional_architecture_path_remains` (`DocsConsistencyTests.cs:32-38`)
**fails the build if `FeatureFlags` reappears in `appsettings.json` or `Program.cs`.** So the dual-path transition of
Phase 3 must be expressed as **config + protocol versioning**, not a feature flag. This is a real constraint on the
implementation cards and is repeated here so it is not rediscovered the hard way.

### 5.4 No performance/balance work (standing ruling)

The streaming discipline is **feasibility/correctness**, not optimisation: without it, 32000² cannot run at all;
with it, the per-tick dynamic cost stays viewport-bounded (unchanged from today). **No tick-budget, P95, allocation or
balance cards** are proposed — the 2026-11-27 ruling keeps performance out of scope until the Godot client reaches
parity (`post-perf-feature-evolution.md:11`). If a phase surfaces a measurement need, it is a **measurement card**
attached to that phase, not an optimisation card.

### 5.5 Cost of being wrong, per direction

Now that the number is ruled, "wrong" is not 32000-vs-256 — it is:

- **If the PO later shrinks the number** (e.g. 32000 → 4096): the cost is **one config change + a world regen + a
  schema-compatible re-issue of fixtures/baselines**. Phases 1–3 (the architecture) survive untouched; only Phase 4's
  *values* and the regenerated artefacts change. This is the deliberate upside of keeping size in configuration.
- **If the PO later grows it further** (32000 → larger): the same — Phases 1–3 are size-independent by construction.
  The only genuinely size-bound remaining literal is chunk size (§4.4).
- **If the migration stops midway** (say after Phase 2, before Phase 3): the sim is **still correct at 256** and the
  default is not flipped, so nothing is broken — the world is just not yet large. A half-done migration is inert, not
  dangerous, **because the flip is the last step**.
- **The one irreversible choice** is **chunk size + the snapshot message shape** (§4.4): pick them wrong and Phases 3
  and 5 re-do the client. Decide them once, in the contract, before Phase 3.

---

## 6. Recommendation

1. **Accept the ruling as binding; do not re-open the number.** Record the unit interpretation (tiles = world units)
   and correct the client's `references/city-map.md` (§0.1).
2. **Sequence as Phases 0→6 (§5.2), with Phase 1 as a hard prerequisite for Phase 4.** Ship the chunked architecture
   under the current 256 default with all existing tests green, then flip the number.
3. **Treat generator + fixtures + baselines + schema as one atomic change** at Phase 4 (extending the PR #105 rule from
   footprint/lot-pitch to size).
4. **Fix the two hidden world-size constants the card did not name:** `SpatialIndex.CreateDefault`'s `halfExtent:1000f`
   (`SpatialIndex.cs:46`) and the API's 64–512 cap (`MapEndpoints.cs:25-27`). Both silently assume a small world.
5. **Decide chunk size + snapshot shape once, in `Genelife.Contracts`, before Phase 3** (§4.4) — the only irreversible
   choice.
6. **No performance or balance work**; streaming is feasibility, and any measurement is a per-phase measurement card
   (§5.4).

## 7. Open items the PO may still want to settle

- **D-A — Move rule at 32000 scale.** `TilePathfinder`'s 4096-tile budget (`TilePathfinder.cs:17`) means a *cross-city*
  path is impossible. Is movement **local** (commute within a chunk / neighbourhood) or must pathing go
  **hierarchical**? This is a gameplay decision with a large implementation difference (§3.3, Phase 6).
- **D-B — Oversight at scale.** Is the whole-city **cartogram/minimap** a server-produced layer or a client-side coarse
  classifier? (Godot does the latter; the browser has no such layer yet — §2.)
- **D-C — One world or many.** With chunks, a 32000² "world" could equally be many smaller named maps stitched by
  travel. The ruling implies one world; confirming avoids building a chunk protocol that then fights an intended
  multi-map design.
- **D-D — Browser client's future.** Does the PixiJS client adopt the chunk protocol (Phase 3/5), or is the browser
  client frozen once the Godot client reaches parity (`godot-client-decision.md` §2–3)? This decides how much of
  Phase 5 the browser needs.

## Sources

Codebase claims are cited inline as `file:line` against `develop` @ `dc3dc6e`. External/companion claims:

[1] `docs/research/building-sizing-and-world-scale.md` (PR #105) — the footprint → lot-pitch → map-extent law (N/N²),
    the migration surface, and the atomic footprint/lot-pitch rule this document extends to size.
[2] `docs/research/terrain-manifest.md` (PR #109) — the art-pack/autotile manifest; the pack is per-sheet
    (`tile_size` 32, sheet 256×192, 48 tiles) and **implies no world extent** — it bounds terrain *variety*, not map
    *size*.
[3] `docs/research/godot-client-decision.md` (PRs #106/#107) — the Godot client is C#, desktop-only, references
    `Genelife.Contracts`/`Genelife.Messages`, never `Genelife.Domain`.
[4] `docs/plans/roadmap-v3/README.md:125,128` — the RLE snapshot format and the standing rule *"Do not hardcode
    256×256 or 32 px in game logic"*.
[5] Client repo `G:\dev\GeneLife.Desktop` — `scripts/City/CityGen.cs` (pure-function + chunk-streamed generator,
    `Size=32000`) and `references/city-map.md` (the world-size ownership rule and the cartogram/streaming pitfalls).
[6] `docs/research/post-perf-feature-evolution.md:11` — the standing deprioritisation of performance work until Godot
    parity.

## Appendix A — measured numbers and how to reproduce them

- **Snapshot size:** `clients/browser/src/test/fixtures/map-snapshot-large.json` = 726,789 bytes (256², 3 buildings);
  run count `grep -o '"x"' … | wc -l` = **7,661** (the header says 7,658 tile runs; 7,661 counts the three building
  `x` fields too).
- **Dense-array arithmetic:** 32000² = 1,024,000,000 tiles; × 1 byte (`TileFlags : byte`) = 1.0e9 B ≈ 1 GB per dense
  array; the signature `StringBuilder` holds one `char` per tile ≈ 2 GB.
- **RLE extrapolation:** 7,661 runs × 15,625 (=32000²/256²) ≈ **1.2e8 runs**; at ~90 B/run (measured from the fixture)
  ≈ **11 GB** whole-map.
- **Camera:** whole-map fit at 256 = 1600/256 ≈ 6.25 px/tile; at 32000 = 1600/32000 ≈ 0.05 px/tile; current
  `minZoom` = 12 (`camera.ts:29`).

## Appendix B — acceptance-criterion cross-check

- **Settles nothing about the number (ruling is in force), answers the consequence:** §0, §1–§6 — the number is taken
  as given.
- **Full migration surface (tests, fixtures, snapshot encoding, baselines, MapGenerator, pathing, camera/bounds):**
  §3.1–§3.7, each a `file:line` row.
- **Streaming/chunking discipline both sides:** §4 (three rules + the shared-constant decision).
- **Cost + safe phased sequencing keeping the sim working:** §5.2–§5.5, with the Phase-1-before-Phase-4 gate and the
  feature-flag constraint.
- **Docs-only, no code/Domain/client change, nothing balance/perf:** the only file this card writes is this document;
  §5.4 states the perf exclusion.
- **Builds on `building-sizing-and-world-scale.md` rather than restating it:** the N/N² law and the footprint→lot
  arithmetic are cited, not repeated.

# Building sizing and world scale — the prerequisite for cutaway interiors

**Author:** @roberto (Solution Researcher) · **Date:** 2026-11-27 · **Branch:** `docs/roberto/building-sizing-and-world-scale`
**Verified against:** `develop` @ `b92fbbd` · **Card:** *"research(sizing): scope proper building sizing and world scale (the cutaway prerequisite)"* (`t_e5c9ae44`)

**Companion documents:** this file extends the house style of the building-interiors dossier
([`building-interiors/`](building-interiors/README.md)) and of
[`post-perf-feature-evolution.md`](post-perf-feature-evolution.md). It answers the follow-on the PO attached to the
Q3 interiors ruling, and it assumes the reader has read `building-interiors/03-architecture-options.md` (Option B/D)
and `04-data-and-sim-model.md` (the room model).

**Scope line.** This is a **scoping/analysis** card: no `.cs` file and no client file is changed, and no map is
regenerated (acceptance criterion 7). Every claim about *this* codebase carries a `file:line`. Every number that was
measured rather than read is marked **measured**, with the harness that produced it in the appendix. Where a number
cannot be established from the repository, the text says so instead of inventing one (criterion 8).

> **Implementation status (landed).** Part 1's MINIMUM VIABLE column and the
> footprint → lot-pitch coupling of §5.1 are implemented. `BuildingFootprints`
> (`ForProperty`/`ForOffice`/`ForSchool`) now derives the minimal near-square
> footprint from the occupant places (2×2 room, ×1.25 circulation, plus the kind's
> common area), clamped to `BuildingFootprints.MaxDimension = 23` (the largest
> minimum-viable value above, the 100-pupil school). `MapGenerationOptions.RoadSpacing`
> is derived from that maximum (`MaxDimension + 1 = 24`), so a lot interior is
> 23×23 and contains every footprint the generator can produce. In the same change
> water is no longer placed inside a lot interior — a lot is a buildable parcel
> (`WorldMap.CanPlace` requires `lot.Bounds.Contains(footprint)`), and at these
> sizes a scattered water tile would make generation throw (Part 1.3) — and
> `MapGenerator.GeneratorVersion` moved 2 → 3. See the card *"map(footprints):
> size the building footprints to hold a real interior, lot pitch in the same
> change"* (`t_6e420a42`) for the landed before/after per kind.

---

## The question, and why it is the cutaway's problem

The PO's Q3 ruling (`building-interiors/08-open-questions-for-the-po.md`, answered 2026-11-27) chose the **roof
cutaway as V1** and added, verbatim:

> *"this also means properly sizing buildings now and a bigger world."*

A cutaway interior is an **index over the building's own footprint tiles** — a room is a rectangle inside
`Placement.Footprint`, and there is no second map (`building-interiors/03-architecture-options.md`, Option B; the
same constraint is restated at `04-data-and-sim-model.md` §5). So the interior **can be no bigger than the
footprint**. Today the footprint is an *envelope sized to content, not a floor plan*
(`Genelife.Domain/Map/BuildingFootprints.cs:5-26`): a 30-unit residential block is a **7×4 = 28-tile** rectangle.
That rectangle cannot hold 30 rooms. Until the footprints are right, a cutaway interior has nothing to be laid out
inside, which is exactly what "properly sized" means and why it is on the critical path.

The two halves of the PO's sentence — *properly sized* and *a bigger world* — are **one change with two
consequences**, and this document quantifies both:

1. **Part 1** derives, per building kind, the footprint an interior actually needs.
2. **Part 2** derives the map size that consequence forces, as arithmetic.
3. **Part 3** names the migration surface (worlds, tests, fixtures, baselines, docs).
4. **Part 4** sequences the work against the interiors dossier.
5. **Part 5** gives a firm recommendation and separates the genuinely PO-owned decisions from it.

---

# Part 1 — What "properly sized" means, quantitatively

## 1.1 Where a footprint is decided (so the reader can reproduce the comparison)

A footprint is produced in **one place**, `Genelife.Domain/Map/BuildingFootprints.cs`, and consumed by every
building creator:

| Kind | Function | Formula | Cap |
|---|---|---|---|
| Residential | `ForProperty(units, isShelter)` (`BuildingFootprints.cs:5-12`) | `W = 4 + clamp(units/10, 0, 4)`, `H = 3 + clamp(units/20, 0, 2)` | 8×5 = 40 |
| Shelter (Civic) | `ForProperty(units, true)` (`:7-8`) | fixed | 4×3 = 12 |
| Office | `ForOffice(capacity)` (`:14-19`) | `W = 4 + clamp(capacity/25, 0, 3)`, `H = 3 + clamp(capacity/50, 0, 2)` | 7×5 = 35 |
| School | `ForSchool(capacity)` (`:21-26`) | `W = 5 + clamp(capacity/100, 0, 3)`, `H = 4 + clamp(capacity/200, 0, 2)` | 8×6 = 48 |
| Store | — | **no footprint function exists** | — |

Callers: `HousingBootstrap.cs:115` (bootstrap stock), `HousingDevelopmentSystem.cs:224` (new construction),
`SimulationEngine.cs:3161,3176,3191` (snapshot projection) and `:7977-7991` (restore/migration),
`CompanyRepository.cs:76`, `OfficeRepository.cs:68`, `SchoolRepository.cs:64`, `PopulationGenerator.cs:470`.
`Store` has no domain component at all (`building-interiors/04-data-and-sim-model.md` §5), so it has neither a
footprint function nor an interior to size — **stated as a gap, not guessed** (criterion 8).

The exact current footprints (measured by calling the real functions; appendix A) are:

```
ForProperty(units=3,6) -> 4x3   ForProperty(units=10,12) -> 5x3   ForProperty(units=20) -> 6x4
ForProperty(units=30) -> 7x4    ForProperty(units>=40) -> 8x5 (capped)
ForOffice(capacity=5,10) -> 4x3 ForOffice(25) -> 5x3  ForOffice(50) -> 6x4  ForOffice(75) -> 7x4  ForOffice(>=100) -> 7x5 (capped)
ForSchool(capacity=22,27,50) -> 5x4   ForSchool(100) -> 6x4   ForSchool(200) -> 7x5   ForSchool(300) -> 8x5   ForSchool(500) -> 8x6 (capped)
```

## 1.2 The interior's demand, and the one free parameter

The room model is fixed by the interiors dossier: a `Room` is a rectangle in the building's **local interior
space**, with a `Capacity` and a `Binding` to a domain container (`building-interiors/04-data-and-sim-model.md`
§1.1). The **number** of sub-spaces is therefore already in the domain — `Property.UnitCount` for residential,
`Office.Capacity` for office seats, `School.Capacity` for pupils (`Property.cs:15`, `Office.cs:14`,
`School.cs:15`). What the domain does **not** carry is a *size* for a room, so this derivation needs exactly one
free parameter, and it must be declared rather than smuggled in.

**The minimum readable room: 2×2 tiles of floor.** This is a *design* parameter, not a repo constant, and here is
its anchor. One tile is one world unit (`MapOptions.cs:17`), and the browser camera renders **1 tile = 32 screen
pixels at its default zoom** (`clients/browser/src/render/camera.ts:19`), with a floor of **12 px/tile** at the
minimum zoom and 128 px/tile at the maximum (`camera.ts:29-30`). A 2×2 room is therefore **64×64 px** at the default
zoom and still **24×24 px** fully zoomed out — against the building label's **10 px** font
(`clients/browser/src/render/map/building-layer.ts:44-53`), that is 2.4–6.4 text-heights. Below 2×2 a "room" is a
single tile, which cannot show a bed or a desk *and* a walkable tile at once; the smallest sub-space the domain
actually asks for is a **1-occupant shelter bed** (`HousingTierPolicy.Capacity(Shelter) = 1`,
`HousingTierPolicy.cs:21-28`). **2×2 is the floor; it is a product/art decision the PO can move (see D4).**

Two more declared parameters, both conservative:

- **Circulation/wall allowance: ×1.25.** Rooms need walls between them and a corridor to reach the door; a quarter
  again over the raw floor area is the cheap end of that. Applied to the whole interior, it makes the *area*
  arithmetic independent of whether the kind is subdivided into many small rooms or a few big ones.
- **Common areas:** residential/shelter lobby 3×3 = 9 tiles; office meeting room 4×4 = 16 plus lobby 9 = 25; school
  hall 4×4 = 16.

So, per kind:

```
area_min = ceil(1.25 × 4 × places) + common
   places = UnitCount (residential / shelter beds)  or  Capacity (office seats / school pupils)
comfortable = 2 × area_min          # a roomy interior, ~1.41× the linear dimension
footprint   = the near-square W×H that fits area_min   # rooms tile a square envelope best
```

## 1.3 The per-kind table

Representative inputs are the numbers the generator itself uses, not invented ones: a **30-unit basic block**
(`HousingBootstrap.cs:28`), a **6-bed shelter** (`:32`), a **10-seat office** (the company headquarters,
`CompanyRepository.cs:62-66`, and the generator's `maxSeatsPerCompany = 10`, `PopulationGenerator.cs:498`), a
**50-seat office** (`Office.cs:22` default), a **27-pupil school** (`PopulationGenerator.cs:429`) and a
**100-pupil school**.

| Kind (representative input) | Interior must hold | Current footprint (source) | **Minimum viable** | **Comfortable** | Area growth (min ÷ current) |
|---|---|---|---|---|---|
| Residential — 30 units (`HousingBootstrap.cs:28`) | 30 unit-rooms + lobby | **7×4 = 28** (`ForProperty(30)`) | **13×13 = 169** | **18×18 = 324** | **×6.0** |
| Shelter/Civic — 6 beds (`HousingBootstrap.cs:32`) | 6 rooms + lobby | **4×3 = 12** | **7×6 = 42** | **9×9 = 81** | **×3.5** |
| Office — 10 seats (`CompanyRepository.cs:65`) | 10 seat-places + meeting + lobby | **4×3 = 12** (`ForOffice(10)`) | **9×9 = 81** | **13×12 = 156** | **×6.8** |
| Office — 50 seats (`Office.cs:22`) | 50 seat-places + meeting + lobby | **6×4 = 24** (`ForOffice(50)`) | **17×17 = 289** | **24×23 = 552** | **×12.0** |
| School — 27 pupils (`PopulationGenerator.cs:429`) | 27 pupil-places + hall | **5×4 = 20** (`ForSchool(27)`) | **13×12 = 156** | **18×17 = 306** | **×7.8** |
| School — 100 pupils | 100 pupil-places + hall | **6×4 = 24** (`ForSchool(100)`) | **23×23 = 529** | **33×32 = 1056** | **×22.0** |
| Store | no domain component, no footprint function | — | — | — | n/a |

*(Areas are exact; the W×H is the smallest near-square rectangle that holds the area. Office/school rows count
**seat/pupil places**, not private rooms — a 50-seat office is one or two open-plan floors, and the ×1.25
circulation factor already absorbs the difference, which is why the area is the same either way.)*

**What the table says, in one line.** The current footprints are an **envelope for the *count* of a building, not a
floor plan for its *contents*** — every kind needs **~3.5× to ~22× more area**, i.e. a **linear growth of ~1.9× to
~4.7×**. The residential block and the school the generator actually creates need **~6–8× the area**, which is the
number the world-size arithmetic in Part 2 has to carry.

**Consequence for the lot, before the map.** A building must fit entirely inside one lot — `CanPlace` requires
`lot.Bounds.Contains(footprint)` (`Genelife.Domain/Map/WorldMap.cs:122-128`, `MapLot.cs:16-22`). The current lot
interior is **11×11** (Part 2). A minimum-viable residential footprint is **13×13**: it **does not fit the current
lot**. So the footprint change is *inseparable* from a lot-pitch change, and a footprint that cannot find a lot
makes world generation **throw**, not degrade — `HousingBootstrap.cs:118-122`, `CompanyRepository.cs:78-86`,
`SchoolRepository.cs:66-72`, `PopulationGenerator.cs:472-474` all raise `InvalidOperationException` on a failed
placement. That coupling is the reason the recommendation in Part 5 ships them as one change.

---

# Part 2 — How much bigger the world must be

## 2.1 The map, the lot, and the one-building-per-lot rule

- The world is a **256×256 tile map centred on the origin** by default (`MapOptions.cs:15-16`); the admin form
  defaults to **256×256, population 500** (`Genelife.Web/Pages/Map.razor:429-432`), and the API accepts **64–512**
  per side and up to **5,000** population (`Genelife.API/Endpoints/Wolverine/MapEndpoints.cs:25-27`).
- Roads are carved every `RoadSpacing` tiles (`Genelife.Domain/Map/MapGenerationOptions.cs:7` = **12**), and a lot
  is the rectangle between four roads: `(roadX+1, roadY+1) … (roadX+spacing−1, roadY+spacing−1)`
  (`Genelife.Domain/Map/MapGenerator.cs:116-124`). So **lot interior = (RoadSpacing − 1)²** = **11×11 = 121 tiles**
  at the default.
- A generated building is always anchored at its lot's **top-left corner** — `TryChooseAnchor` computes
  `candidateAnchor = (lot.Bounds.MinX, lot.Bounds.MinY)` and never tries another anchor
  (`Genelife.Domain/Map/WorldMap.cs:356-383`). A second building in the same lot would land on the same corner and
  be rejected as `Occupied` (`WorldMap.cs:142-150`). **Therefore one building per lot in generated worlds**, and the
  lot count is the building ceiling.

**Measured lot capacity** (appendix A; the model was validated against the real `MapGenerator` at nine points,
all matching):

| Map | RoadSpacing | Lot interior | Lots (= building ceiling) |
|---|---|---|---|
| 128×128 | 12 | 11×11 | 100 |
| **256×256 (today)** | **12** | **11×11** | **400** |
| 256×256 | 18 | 17×17 | 196 |
| 256×256 | 20 | 19×19 | 144 |
| 384×384 | 18 | 17×17 | 400 |
| 448×448 | 20 | 19×19 | 484 |
| 512×512 | 24 | 23×23 | 400 |

## 2.2 The scaling law

Let a building's footprint grow by a linear factor **N** (both dimensions). Then:

1. its **area** grows by **N²**;
2. the **lot** must grow by **N** to keep containing it, so `RoadSpacing` grows by **N**;
3. lots per axis ≈ `mapSize / RoadSpacing`, so lots per axis **falls by N** and total lots **fall by N²**;
4. to hold the **same number of buildings B**, the map's linear size must grow by **N** — i.e. **map area grows by
   N²**.

That last line is the whole answer to "how much bigger": *the map's area must grow by the square of the linear
building growth, to keep density and building count constant.* It is the same statement as "the lot is the unit of
density": if the unit grows by N and you want the same number of units, you need N² the area.

## 2.3 How many buildings the world actually has (so "same number" is a real number)

Building count for a generated population P (from `PopulationGenerator.cs:122-133,422-444,486-596`):

```
workCount   = 0.546 × P                       # adults 0.78P − retirees 0.20·adults − elders 0.10·adults
seats       = round(workCount × 0.95)         # employmentShare = 1 − SeedUnemploymentRate(0.05), :493
companies   = ceil(seats / 10)                # maxSeatsPerCompany = 10, :498  → 1 HQ office each (CompanyRepository.cs:62)
landlords   = companies                       # CompanyLandlords(), :617-629
basicProps  = companies                       # basic units split one property per landlord, :605-607
buildings   = companies(offices) + basicProps + 3 (comfortable+luxury+shelter) + 2 schools
```

which gives **≈ 47 buildings at P = 500** (the admin default), **≈ 213 at P = 2,000**, and **≈ 411 at P = 5,000**
(the API maximum).

**This exposes a limit that already exists today:** the 256×256 default holds **400 lots**, and the API's maximum
population needs **≈ 411 buildings** — the current map is already at its ceiling for the largest allowed world. Any
increase in footprint can only make that worse, which is why the world-size decision is not optional once the
footprints grow.

## 2.4 The options, with cost and what each forecloses

Target: the *comfortable* footprint from Part 1 needs a lot of **18–19** (residential/school 18), i.e.
`RoadSpacing ≈ 19-20`; the *minimum-viable* footprint needs a lot of **13**, i.e. `RoadSpacing ≈ 14`.

| # | Option | Map | RoadSpacing | Lot | Lots | vs today | Hosts ≈213 (P=2,000)? |
|---|---|---|---|---|---|---|---|
| A0 | **Today** | 256 | 12 | 11×11 | 400 | 1.00× | yes |
| A1 | **Same map, bigger blocks (minimum)** | 256 | 14 | 13×13 | 324 | 0.81× | yes |
| A2 | **Same map, bigger blocks (comfortable)** | 256 | 20 | 19×19 | 144 | 0.36× | **no** (144 < 213) |
| B1 | **Bigger map, same capacity (minimum)** | 384 | 18 | 17×17 | 400 | 1.00× | yes (area **2.25×**) |
| B2 | **Bigger map, same capacity + headroom (comfortable)** | 448 | 20 | 19×19 | 484 | 1.21× | yes (area **3.06×**) |
| B3 | **Bigger map at the API maximum (comfortable)** | 512 | 24 | 23×23 | 400 | 1.00× | yes (area **4.00×**) |
| C | **Vertical instead of horizontal** | 256 | 12 | 11×11 | 400 | 1.00× | yes (no map change) |

- **A1** — smallest change: raise `RoadSpacing` 12 → 14 and grow the footprints to the minimum. Cost: −19% building
  capacity, and the interiors are only *minimum-viable*, not comfortable. Forecloses nothing structurally.
- **A2** — properly sized buildings on the current map. Cost: capacity collapses to **144**, below the P=2,000
  target (213) and far below the API maximum (411). **Forecloses large populations** unless the population ceiling
  is cut too.
- **B1** — keeps today's 400-building capacity with minimum-viable buildings; the map goes 256 → 384 (area 2.25×).
  Cost: a bigger static map; the 5,000-population world (411 buildings) still exceeds 400.
- **B2** — keeps capacity *with headroom* (484 ≥ 411) and comfortable interiors; map 256 → 448 (area 3.06×). Cost:
  the largest static payload of the horizontal options.
- **B3** — comfortable interiors at today's capacity, using the API's maximum 512 (area 4×, `MapEndpoints.cs:26`).
  Cost: the biggest map; no further headroom without raising the API cap.
- **C** — leave footprints and the map alone and stack the rooms **vertically** (the PO's V2 multilevel cutaway).
  Cost: it needs the V2 model, which does not exist yet. Forecloses the V1 single-floor "see the whole house at
  once" cutaway, and it does not fix the *footprint* the V1 cutaway is bounded by — it only postpones the
  horizontal growth to V2.

## 2.5 The performance implication (stated, and stopped there)

Per the PO's 2026-11-27 ruling, performance work is out of scope until the Godot client reaches parity
(`docs/research/post-perf-feature-evolution.md:11`). This document therefore **states** the implication and proposes
**no** tick-budget work:

- **Static payload.** The map snapshot is run-length encoded per row and sent once per connect
  (`Genelife.Contracts/Dtos/MapSnapshot.cs:8-29`; `building-interiors/01-problem-and-current-state.md` §2.4). The
  committed 256×256 fixture is **726,789 bytes / 7,658 tile runs** (`clients/browser/src/test/fixtures/map-snapshot-large.json`,
  **measured**). Runs scale with area × runs-per-row, so area ×2.25–4 implies roughly **1.6–2.9 MB** for the same
  layout style — a bigger once-per-connect payload, not a per-tick cost.
- **Tile grid and pathfinding.** The layout is a flat `TileFlags[]` of `bounds.Area` (`MapGenerator.cs:25`), and the
  tile grid, `WorldMap` occupancy and the pathfinder all grow with area (2.25×–4×). `Reconcile`/`Rebuild` iterate
  every placement and tile (`WorldMap.cs:385-431`).
- **Viewport-scoped work is bounded, not area-scaled.** `AoiFilter`/culling/`MovementIndex` are per-viewport, so a
  bigger map does not by itself grow the per-tick dynamic cost; the static footprint and the grid do grow.

The action that follows from this is a *measurement card* if and when the PO picks a scale — **not** an optimisation
card now, and not a footprint change made for performance reasons.

---

# Part 3 — The migration surface

## 3.1 Does the existing map regenerate, or is it kept?

**Both, depending on the world.** The map is persisted, not recomputed, and it is regenerated only on an explicit
world reset:

- **Persisted.** `PersistedMap` carries `Width, Height, TileSize, Tiles, CanonicalSignature, Lots, SpawnPoints`
  (`Genelife.Application/Persistence/PersistedWorldState.cs:641-653`), and `PersistedPlacement` carries
  `X, Y, Width, Height, EntryX, EntryY, Rotation` (`:671-682`), under schema version **16** (`:5`).
- **Regenerated on reset.** `SimulationManager.CreateMap` builds a new `MapOptions` and calls `engine.Initialize`,
  which calls `MapContext.Generate` (`SimulationEngine.cs:183,302`; the reset path is
  `SimulationManager.cs:180-230`), bumping `Map.Version` and broadcasting a `WorldResetMessage`
  (`Genelife.Contracts/Dtos/MapCreation.cs:33-40`).
- **Restored otherwise.** `SimulationEngine.EnsurePlacements` (`SimulationEngine.cs:7940-8010`) is the split point:
  a building **with** a persisted placement is restored *verbatim* via `RestorePlacement`
  (`BuildingPlacementService.cs:82-105`) — **including its old `Width`/`Height`** — while a building **without** one
  gets its footprint recomputed from the domain and re-placed (`:7977-7991`).

**What that means for a footprint change.** A change to `BuildingFootprints` does **not** rewrite persisted worlds:
their old, small footprints survive a restart. Only new worlds, new construction, and migrated buildings get the new
footprints. Crucially, `RestorePlacement` validates bounds and occupancy but **not lot fit** (`:87-94`), so old small
placements keep restoring even after the lot pitch changes; the failure mode is the other way round — a **new**
footprint that no lot can contain makes generation throw (Part 1.3). So:

> **A footprint change and a lot-pitch change must ship together, and an existing persisted world keeps its old
> geometry until the player resets it.** There is no automatic re-layout, and none is proposed here.

## 3.2 The tests and fixtures that would change

**Domain tests that hardcode a footprint or a map size** (they would need the new numbers):

- `Genelife.Domain.Tests/Map/MapFoundationTests.cs:59-64` — asserts a `4×3` footprint occupies **12 tiles**;
  `:17-21` builds a 64×64 map with `RoadSpacing = 10`.
- `Genelife.Domain.Tests/Map/BuildingRelocationTests.cs:30,157,185` — `4×3` / `3×3` / `4×3` footprints inside a
  64×64/`RoadSpacing 10` map; `FindDestination` (`:35-60`) scans a fixed `4×3`-shaped window.
- `Genelife.Domain.Tests/Services/MapCreationTests.cs:20` and
  `Genelife.Api.IntegrationTests/Endpoints/Wolverine/MapCreationEndpointTests.cs:44,87` — assert **128×128**
  create requests.
- `Genelife.Domain.Tests/Map/MapSnapshotTests.cs:38` — asserts `Placements.Count == snapshot.Buildings.Count`.
- `Genelife.Domain.Tests/Map/MapMigrationTests.cs:14-52` — the V4 backfill, which re-places a map from scratch.
- `Genelife.Domain.Tests/Marten/MapProjectionCoverageTests.cs:26-33` — every durable map event must have a
  projection; unchanged *unless* a new map/layout event is introduced with the resize.
- Layout-independent but footprint-adjacent (small explicit footprints, would only change if they start asserting
  on generated worlds): `Genelife.Domain.Tests/Snapshots/GenosBuildingSnapshotTests.cs:45,124`,
  `Social/PresenceTests.cs:37`, `Social/VenueTests.cs:220`, `Social/ModerationTests.cs:47`,
  `Genelife.Performance.Tests/Performance/SocialSoakTests.cs:65`.
- `Genelife.Web.Tests/Pages/MapTests.cs:11-33` — stubs a **32×32** map status.

**Client fixtures and baselines** (the layout is drawn from the map snapshot, so a resize changes them):

- `clients/browser/src/test/fixtures/map-snapshot-large.json` (**256×256**, 3 buildings, 7,658 tile runs, 726,789 B)
  and `map-snapshot.json` (**32×32**); `map-delta.json` follows the same shape. The hand-mirrored contract types
  fail loudly if the shape drifts (`building-interiors/01-problem-and-current-state.md` §2.4).
- Visual baselines: `clients/browser/e2e/visual/appearance.spec.ts-snapshots/appearance-world-visual-chromium-win32.png`
  and `clients/browser/e2e/visual/stress-density.spec.ts-snapshots/stress-density-visual-chromium-win32.png` —
  buildings are drawn as footprint rectangles by `clients/browser/src/render/map/building-layer.ts:63-99`, so a
  footprint change moves those pixels.

**Documentation that carries coordinates or scale** (must be updated with the numbers, not left stale):

- `docs/plans/ROADMAPS.md:37` ("256×256 default"), `docs/plans/roadmap-v3/README.md:125,128,184` (the RLE row
  format and the explicit rule *"Do not hardcode 256×256 or 32 px in game logic"* — the existing convention this
  change must respect).
- `docs/research/building-interiors/01-problem-and-current-state.md:28` and
  `docs/research/building-interiors/05-contract-and-server.md:75` — both state "256×256 = 65,536 tiles".
- The docs guard itself, `Genelife.Domain.Tests/Docs/DocsConsistencyTests.cs:13,142` (`Category=Documentation`),
  does **not** assert map size, so it stays green through a resize; it is re-run here as the card's guard
  (appendix B).

---

# Part 4 — Sequencing against the interiors work

The interiors dossier split the work into four items, three client-agnostic and one client-specific
(`building-interiors/01-problem-and-current-state.md` §4):

1. the **space model** (`Room` / `Interior` / `InteriorTemplate` types and their bindings),
2. the **interior payload/scope** (`InteriorPlan` DTO, `InteriorLocation` on `GenosStatus`, the `GenosesEqual` trap),
3. the **transition contract** (entry authority reused from `ApplyUseBuildingIntent`),
4. the **client renderer** (the roof cutaway and room drawing).

**What must come first: the sizing (this card's subject).** The V1 cutaway is bounded by the footprint, so the
footprint function, the lot pitch and the map size are the envelope that items 1 and 4 are authored *into*. Landing
the cutaway before the resize means authoring room rectangles and a client layout against an envelope that is about
to change.

**What can proceed in parallel — the claim in the card, confirmed and refuted in parts.** The card says the room
model and the scoped payload are footprint-independent. That is **true of the types and the contract, and false of
the authored template geometry**:

- **Footprint-independent (can proceed in parallel):** the `Room` / `Interior` / `InteriorTemplate` *types* and the
  `BuildingKind → domain container` binding (`building-interiors/04-data-and-sim-model.md` §1, §3); the
  `InteriorPlan` DTO and the `RequestInteriorPlan` scope (`building-interiors/05-contract-and-server.md` §3.2, §5);
  the `InteriorLocation` field and its mandatory entry in `GenosesEqual` (the sharpest trap, `05` §3.1); the entry
  authority, which already exists (`SimulationEngine.cs:1460-1515`); and the *client renderer's* roof-toggle
  plumbing. None of these depends on how big a footprint is — they are parameterised by the building's own
  `UnitCount`/`Capacity`.
- **Footprint-dependent (must wait for the final envelope):** the *authored template geometry* — the room rectangles
  inside the envelope (`building-interiors/04-data-and-sim-model.md` §1.1 defines `Room.Bounds` as a rectangle in
  local interior space) — and the client's room-layout/camera framing. Authoring these before the resize is the
  wasted work.

**What is wasted if the order were reversed** (cutaway interior first, resize second): the authored room rectangles
for all five templates, the client room-layout assumptions and camera framing, and the interior visual baselines —
re-authored from scratch. The *types*, the *DTOs*, the *payload scope* and the *entry authority* survive a resize
untouched, exactly as they survive a client switch (`building-interiors/07-recommendation.md` R3).

**The one hard coupling.** The footprint change cannot ship alone: a footprint that fits no lot makes world
generation throw (Part 1.3). So the footprint function, `MapGenerationOptions.RoadSpacing` and (if the PO grows the
world) `MapOptions.Width/Height` are a **single atomic change**, and that change must precede any template geometry.

---

# Part 5 — Recommendation and the PO's decisions

## 5.1 The recommendation (firm)

1. **Size to the interior's minimum, not to comfort, for V1.** Take the `minimum viable` column of the Part-1 table
   as the V1 target and keep `comfortable` as a documented V2/V3 target. Comfortable sizing multiplies the world
   cost (Part 2.4) for headroom the first generic interiors cannot use; the minimum already makes a cutaway room
   readable.
2. **Ship the footprint change and the lot-pitch change as one change**, because a footprint that fits no lot
   throws at generation (Part 1.3). This is not a preference; it is a correctness constraint.
3. **Order: sizing first; the model/contract in parallel; the cutaway last.** The `Room`/`Interior`/`InteriorTemplate`
   types, the `InteriorPlan` scope, `InteriorLocation` + `GenosesEqual`, and the entry authority are
   footprint-independent and can be built while the sizing lands. The **authored template geometry and the client
   room layout are the only pieces that must wait** for the final envelope (Part 4).
4. **Keep the map size in configuration, never in game logic.** The repo already states this rule
   (`docs/plans/roadmap-v3/README.md:128`); a resize is a `MapOptions`/`MapGenerationOptions` change plus a
   regeneration, not a scatter of new constants.
5. **Do not grow the world for performance reasons, and do not propose tick-budget work.** State the implication
   (Part 2.5) and stop; the 2026-11-27 ruling forbids the rest.

## 5.2 The decisions that are genuinely the PO's

These are product/scope decisions this analysis cannot make; each is stated with its trade-off. **The scale — how
big to go — is the first and the PO's to make.**

- **D1 — How big: minimum-viable or comfortable?** Minimum (~linear ×1.9–2.8, e.g. residential 7×4 → 13×13) keeps
  the world small and the interiors readable but plain; comfortable (~linear ×2.5–4.7) buys roomy interiors at
  3–4× the map area. *Trade-off: interior quality vs world size.*
- **D2 — Grow the map, or keep the map and accept fewer buildings?** A1 (256 map, `RoadSpacing` 14) keeps the map
  but cuts capacity ~19% and caps the population well below the API's 5,000; B1/B2/B3 grow the map (256 → 384/448/512,
  area 2.25×–4×) to keep or raise capacity. *Trade-off: world scale vs density and the population ceiling.*
- **D3 — Horizontal or vertical?** Grow footprints horizontally (A/B options) for a V1 single-floor cutaway, or
  keep footprints and stack rooms vertically in the V2 multilevel cutaway (Option C). *Trade-off: V1 simplicity vs
  deferring the growth to V2 and losing the "see the whole house at once" cutaway.*
- **D4 — Is 2×2 the right minimum readable room, and ×1.25 the right circulation allowance?** Both are declared
  design parameters (Part 1.2), anchored to the 32 px/tile default zoom. A smaller room shrinks every footprint and
  the map with it; a larger one grows them. *Trade-off: readability vs world size — an art decision.*
- **D5 — Does the population ceiling move with the map?** The API already allows 5,000 population, which needs
  ≈411 buildings against the 256-map ceiling of 400 (Part 2.3). Either the map grows, or the ceiling is lowered to
  match a fixed map. *Trade-off: maximum world population vs a fixed map size.*

---

## Sources

External claims in this document are prior-art and rendering facts already sourced in the companion dossier; no new
external source is introduced.

[1] `docs/research/building-interiors/02-prior-art.md` — prior art (Project Zomboid rooms as rectangles on one map;
    the instanced-MMO model), with its own numbered sources, including the PZ cell sizes (256×256 in Build 42).
[2] `docs/research/building-interiors/03-architecture-options.md` — Option B/D, the cutaway bounded by the footprint.
[3] `docs/research/post-perf-feature-evolution.md:11` — the 2026-11-27 ruling that performance work is out of scope
    until the Godot client reaches parity.

## Appendix A — measured numbers and the harness that produced them

**Footprints and lot capacity** were produced by a throwaway console project that references the real
`Genelife.Domain` assembly and calls the real code — `BuildingFootprints.ForProperty/ForOffice/ForSchool` and
`MapGenerator.Generate(MapOptions, MapGenerationOptions)` — then counts `GeneratedMap.Lots`. The lot-count model
reused in Part 2 was validated against that output at nine points (64/128/256/512 at `RoadSpacing 12`, and
256 at `RoadSpacing 16/18/20/24/30), **all matching**. The project lives outside the repository and no `.cs` file in
the repo was touched (criterion 7).

Key measured values:

```
BuildingFootprints: ForProperty(30)=7x4(28)  ForProperty(6,shelter)=4x3(12)  ForOffice(10)=4x3(12)
                    ForOffice(50)=6x4(24)     ForSchool(27)=5x4(20)           ForSchool(100)=6x4(24)
Lot grid (lot interior = (RoadSpacing-1)^2):
  64x64   sp12 -> 16 lots (4x4)      128x128 sp12 -> 100 lots (10x10)
  256x256 sp12 -> 400 lots (20x20)   256x256 sp14 -> 324   256x256 sp18 -> 196
  256x256 sp20 -> 144                256x256 sp24 -> 100   256x256 sp30 -> 64
  384x384 sp18 -> 400                448x448 sp20 -> 484   512x512 sp24 -> 400   512x512 sp12 -> 1764
```

**Payload** was measured by reading the committed fixture `clients/browser/src/test/fixtures/map-snapshot-large.json`
(256×256, 3 buildings, 7,658 tile runs, 726,789 bytes, pretty-printed JSON).

## Appendix B — the docs guard, run green

```
$ dotnet run --project Genelife.Domain.Tests/Genelife.Domain.Tests.csproj --no-launch-profile -- -trait "Category=Documentation"
=== TEST EXECUTION SUMMARY ===
   Genelife.Domain.Tests  Total: 7, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 0.157s
```

## Appendix C — acceptance-criterion cross-check

- **AC1 — the document exists, five areas, dossier rigour:** this file; `file:line` throughout, sources in
  `## Sources`.
- **AC2 — per-kind current vs minimum vs comfortable, current traced:** §1.3 table; current values from
  `BuildingFootprints.cs` via appendix A.
- **AC3 — world size as arithmetic:** §2.2 (the N/N² law), §2.3 (the building count), §2.4 (the measured option set).
- **AC4 — migration surface names real files and tests:** §3.1–§3.2.
- **AC5 — firm recommendation, PO decisions separated:** §5.1 vs §5.2.
- **AC6 — memanto holds the verdict, the key numbers, the constraint, with correct types:** written in the same
  session as `--source genelife`.
- **AC7 — no code changes, docs guard green:** appendix B; no `.cs`/client file changed by this card.
- **AC8 — unestablishable numbers stated, not invented:** `Store` has no footprint function (§1.1); the room minimum
  and circulation factor are declared design parameters, not repo constants (§1.2).

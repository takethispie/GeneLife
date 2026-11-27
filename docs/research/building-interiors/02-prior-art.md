# 02 — Prior art: how shipping sims handle interiors

**Verified against:** public documentation, wikis and developer statements; each claim carries a numbered source at
the bottom of this file.

## How to read this file

For each shipped game or family, the question is the same three-part one from [01](01-problem-and-current-state.md):
*what is the space model, what is the transition, who owns "inside"*. Each entry ends with **transfers** (what
GeneLife can borrow) and **does not transfer** (why the analogy stops).

The selection is deliberate: two top-down colony sims whose interior is *the same map* (RimWorld, Prison Architect),
one life sim that made the boundary a **loading screen** (The Sims), one whose interior is a **z-level** (Dwarf
Fortress), one that models **named rooms on one continuous map** (Project Zomboid), the **instanced-interior MMO
family** (WoW / FFXIV / ESO), and one successful sim that **deliberately has none** (Cities: Skylines).

---

## RimWorld — a room is a flood-fill region on the one and only map

RimWorld has no separate interior scene and no loading screen. A **room** is "a space fully enclosed by impassable
objects such as walls, doors, vents, natural rock or coolers"; corners need not be filled; the region is found by a
flood fill when walls/doors/floors change, and classified into a role (bedroom, kitchen, …) by **its size and
contents** — a bed in a walled space becomes a bedroom, a stove becomes a kitchen. Room size is capped (36 map
regions or 5,184 tiles) and room stats (wealth, beauty, space, cleanliness) feed mood.[1][2][3]

- **Transfers.** The *derivation* pattern: interiors need not be authored as data if the space model can derive
  rooms from an enclosure rule plus contents — and the enclosure rule is exactly what `WorldMap` already computes
  for occupancy (`Genelife.Domain/Map/WorldMap.cs:59-64,89-95`). It also shows a cheap **room-role** concept (a room
  is what its contents make it) that maps onto GeneLife's `BuildingKind` + component shape.
- **Does not transfer.** RimWorld's map is one tile grid with walls as tiles; GeneLife's building is a single
  `Placement` footprint whose interior tiles are not modelled at all (`Placement.cs:7-29`). RimWorld also *runs every
  pawn's room logic every tick*, which the 2026-11-27 performance ruling forbids ([01 §3](01-problem-and-current-state.md)).
  The flood-fill is the idea to borrow; running it per-pawn is the part to leave behind.

## Prison Architect — "enclosed" is a first-class, checked requirement of a designated room

Prison Architect lets the player *designate* a room type over an area, but the designation only becomes **active**
when its requirements are met — and `Enclosed` "prevents a Room from becoming active until it has walls and at least
one door on all adjacent tiles". A room that fails to enclose shows a red warning. The save format makes the model
explicit: every cell records an **`Ind` (Inside) boolean** and, if it belongs to a room, a `Room.i` id; the rooms
section stores each room by id and type.[4][5][6]

- **Transfers.** Two ideas. (a) **"Enclosed" is testable and belongs on the server** — a room is not valid until the
  enclosure rule passes, exactly as `WorldMap.CanPlace` validates a footprint before it is placed
  (`WorldMap.cs:97-160`). (b) **A per-tile `Inside` flag plus a room id is a sufficient serialisation** — the save
  format is a worked example of the *minimum* data an interior needs on the wire: one boolean per tile and one id
  per room.
- **Does not transfer.** Prison Architect's interior *is* the map — there is no boundary to cross at all, so it says
  nothing about the transition UX or about scoping an interior payload. It also pays its cost with a small,
  bounded map; GeneLife places thousands of buildings ([05](05-contract-and-server.md)).

## The Sims — the boundary is a *loading screen*, and off-lot sims keep simulating

The Sims 4 loads **one lot at a time**; walking onto a different lot in the same neighbourhood incurs a loading
screen (public space ↔ the active lot does not), while sims in unloaded lots continue to simulate in the
background.[7][8] This was an explicit engineering choice for performance and platform reach and is why a mod that
loads several lots at once is newsworthy.[9] The Sims 3, by contrast, loaded the whole open world at once.[9]

- **Transfers.** This is the canonical **"the map keeps running while you are inside"** model, and it is the one
  GeneLife should copy: the world simulation is continuous and server-owned; entering a building changes *only what
  the client is shown*, not whether the world ticks. The Sims' own division ("time passes for off-lot sims; the
  active lot is the loaded one") is precisely the "interiors refine presence for the player's scope only" split in
  [01 §3](01-problem-and-current-state.md).
- **Does not transfer.** A loading screen is a *client* device; it is invisible to the server model, so it does not
  answer who owns "inside". The Sims' lot is also a hand-authored player-built structure, whereas GeneLife's
  buildings are generated from parameters (`BuildingFootprints.cs`).

## Dwarf Fortress — the interior is a z-level of the same continuous voxel world

Dwarf Fortress gives its world a third dimension: content is on **z-levels**, the player views one level at a time,
and "inside/outside", "dark/light" and "subterranean/above-ground" are per-tile attributes. Rooms are *defined* by
placing furniture and using the `q` designation over an area; walls span tile sections across levels.[10][11][12]

- **Transfers.** The strongest transfer here is negative-to-header: Dwarf Fortress proves a **continuous,
  transition-free model is achievable** if the interior is modelled as *more of the same grid* (here, another axis)
  rather than a separate scene. That is the "sub-grid / overlay" option in [03](03-architecture-options.md), and
  Dwarf Fortress is the existence proof that it can feel seamless.
- **Does not transfer.** DF simulates the interior for *every* dwarf every tick on a single-player machine with no
  network contract; GeneLife must ship a server snapshot to many clients at 25 Hz ([05](05-contract-and-server.md)).
  DF is an existence proof of the *feel*, not of the *cost model*.

## Project Zomboid — rooms are named rectangles over one continuous tile map

Project Zomboid invests in interiors without a scene transition: a `RoomDef` is a **rectangle** (`x, y, x2, y2`)
with an `isInside(x, y, z)` test, rooms are named by type ("kitchen", "bedroom", …) largely to drive loot spawns,
and the map is authored in cells of 300×300 (Build 41) / 256×256 (Build 42) tiles. The building is *part of the one
world*, and the room is an index over that world.[13][14][15]

- **Transfers.** PZ is the closest shipped analogue to GeneLife's actual topology: **one continuous server tile
  map, buildings as rectangles on it, rooms as a labelled index over the tiles inside those rectangles**. A room
  there is *data about the map*, not a second map — which is exactly the "sub-grid/overlay" option's promise, and
  PZ shows it shipping. Its cell size (256×256 in B42) is, coincidentally, GeneLife's whole map size
  (`MapOptions.cs:14-15`).
- **Does not transfer.** PZ's rooms exist mainly for **loot distribution**, not for a player standing in them; its
  camera and art are built for seeing into a building continuously, which GeneLife's roof-rendering
  (`building-layer.ts`) is not. It also does not answer the "thousands of buildings, one client payload" question.

## Instanced MMO housing (WoW, FFXIV, ESO) — the interior is a *separate instance*, reached through a loading screen

The dominant MMO pattern is an **instanced interior**: FFXIV, ESO and now WoW give each home an *interior that is a
separate instance from the exterior*; you cross a boundary (a door, a portal, a loading screen) and the game loads a
different, usually small, space. Players and developers describe the trade-off directly: the interior "is instanced
and separate from the exterior", and it exists because "if the houses weren't phased/instanced, the zone would
basically get unplayable" with every player's placeables loaded at once.[16][17][18] Some systems create the instance
on demand and close it when empty; others keep the ward always-online.[17]

- **Transfers.** This is the direct precedent for the **"separate map/scene with a transition"** option in
  [03](03-architecture-options.md), and — more importantly — the *reason* it exists is the same reason GeneLife will
  feel it: **loading the interior only for the building you are in bounds the payload and the render cost**, which is
  exactly the scale constraint that makes "ship a plan for every building" impossible ([05](05-contract-and-server.md)).
  The "created on demand, closed when empty" variant is the resource model that fits a server-owned instance.
- **Does not transfer.** These are hand-authored, often player-decorated spaces in a 3D client with a much heavier
  asset budget; GeneLife's interiors would start generic and parameterised. The MMO analogy tells you the
  *boundary and the scoping* are normal and accepted by players; it does not tell you the interiors should be as
  detailed.

## Cities: Skylines — the deliberate counter-example: an efficient sim with *no* interiors at all

Cities: Skylines has **no building interiors** and no way to enter one. Citizens ("cims") are **abstractions**:
"cims are abstracted into patterns of movement, traffic, and retail and labor patterns"; buildings, roads and cims
"largely do not matter" as individuals, and the game is explicit that its agents are a compromise — "a lower real
population count means that the game engine can simulate visually each cim … even if that cim's behavior is more
akin to a group of a hundred". When players ask for interiors, the community answer is that interiors "don't align
with the primary purpose of the game" and would demand "immense labor for something that isn't central".[19][20]

- **Transfers.** The counter-example is load-bearing: it establishes that **interiors are a genre/scope decision,
  not a technical necessity**, and that a sim can be successful while simulating agents as abstractions. It is the
  evidence that "we simulate presence, not rooms" is a legitimate product position — which is why
  [08](08-open-questions-for-the-po.md) asks the PO to confirm interiors are wanted for the *player* at all.
- **Does not transfer.** Skylines is a god-view city builder where the player never embodies an agent; GeneLife is a
  life-sim where the player drives one genos and already has an entry flow
  (`building-selection.ts`). The moment a game asks the player to *be* someone who walks through a door, the
  abstraction stops being enough — which is the position GeneLife is in.

---

## Synthesis: what the prior art actually settles

| Question | What the prior art says | Consequence for GeneLife |
|---|---|---|
| Is there a dominant model? | No. One-map models (RimWorld, Prison Architect, PZ, DF) and instanced models (Sims, MMOs) both ship at scale | The choice is **product/scope**, decided in [03](03-architecture-options.md)/[07](07-recommendation.md), not a technical forced move |
| Does the world stop when you go inside? | Never. Off-scope agents keep simulating (Sims explicitly) | Entering changes **what the client shows**, not whether the server ticks |
| What is the minimum data? | A per-tile `Inside` flag + a room id (Prison Architect's save); a rectangle + an `isInside` test (PZ) | A room plan can be tiny; the cost is in **how many** you ship, not in one room ([05](05-contract-and-server.md)) |
| Does anyone simulate every agent's room every tick *and* ship it? | No — the one-map sims are single-player and offline (DF, RimWorld) | The player-scoped refinement of [01 §3](01-problem-and-current-state.md) is the only affordable shape |
| Is "no interiors" defensible? | Yes (Cities: Skylines) | The PO must confirm interiors are wanted; see [08](08-open-questions-for-the-po.md) |

The single most important transfer is not a data structure — it is the **seam**: in *every* shipped system, the
server/simulation owns "inside" continuously and the client's job is only to *present* the crossing. That is exactly
the shape GeneLife's authority rule already has ([01 §2.3](01-problem-and-current-state.md)).

---

## Sources

[1] https://rimworldwiki.com/wiki/Rooms — RimWorld Wiki, "Rooms" (enclosure by impassable objects; indoors/outdoors conditions)
[2] https://www.rimworldwiki.com/wiki/Wall — RimWorld Wiki, "Wall" (walls create rooms; corners not required)
[3] https://simgrammar.com/mechanic/room-recognition/ — simgrammar, "Room recognition" (flood fill on change; role scored from contents; 36-region / 5,184-tile cap)
[4] https://prisonarchitect.paradoxwikis.com/Room — Prison Architect Wiki (Paradox), "Room" (designate a space; enclosed requirement)
[5] https://github.com/originalfoo/Prison-Architect-API/wiki/Enclosed — Prison Architect API wiki, "Enclosed" (`Enclosed` requirement blocks a room from becoming active)
[6] https://prisonarchitect.paradoxwikis.com/.prison — Prison Architect Wiki, ".prison" save format (per-cell `Ind` inside flag, `Room.i` id, room types)
[7] https://simscommunity.info/2014/06/15/the-sims-4-simgurugraham-explains-loading-screens/ — SimGuruGraham on Sims 4 loading screens (load between lots, not into public space)
[8] https://simsvip.com/2014/07/02/the-sims-4-loading-screens-the-world-explained/ — SimsVIP, "Loading Screens & The World Explained" (one active lot at a time; off-lot sims continue)
[9] https://simscommunity.info/2026/10/27/sims-4-open-neighborhood-mod-preview/ — Sims Community, "Modder Unlocks Sims 4 Open Neighborhoods" (Sims 4 built around loading screens for performance; Sims 3 was open-world)
[10] https://dwarffortresswiki.org/index.php/Tile — Dwarf Fortress Wiki, "Tile" (tiles span z-levels; wall/floor sections)
[11] https://dwarffortresswiki.org/index.php/v0.31:Dwarf_fortress_mode — Dwarf Fortress Wiki, "Dwarf fortress mode" (per-tile Inside/Outside/Dark/Light attributes; `q` defines rooms)
[12] http://dwarffortresswiki.org/index.php/40d:design_strategies — Dwarf Fortress Wiki, design strategies (3D map; z-distance equals tile distance)
[13] https://projectzomboid.com/modding/zombie/iso/RoomDef.html — Project Zomboid Javadoc, `zombie.iso.RoomDef` (`isInside(x,y,z)`, rectangle getters)
[14] https://pzwiki.net/wiki/Room_definitions_and_item_spawns/R — PZwiki, "Room definitions and item spawns" (named room types drive loot)
[15] https://pzwiki.net/wiki/Mapping — PZwiki, "Mapping" (map cells of 300×300 in B41, 256×256 in B42)
[16] https://us.forums.blizzard.com/en/wow/t/housing-not-good-but-not-terrible/2186087?page=9 — WoW forums (FFXIV/ESO/LOTRO interiors are instanced and separate from the exterior; zone unplayable otherwise)
[17] https://www.reddit.com/r/ffxivdiscussion/comments/1iik4vz/housing_private_neighborhoods/ — r/ffxivdiscussion (instance created on access vs always-online ward; WoW/GW2/ESO comparison)
[18] https://us.forums.blizzard.com/en/wow/t/saddened-by-instanced-housing/2213889?page=5 — WoW forums (interior instanced so it does not share the exterior budget; loading screen is inherent to instancing)
[19] https://www.avclub.com/cities-skylines-is-gentrifiers-dream — AV Club, "Cities: Skylines Is a Gentrifier's Dream" (cims as abstractions; individual entities "largely do not matter")
[20] https://www.reddit.com/r/CitiesSkylines/comments/1rnm47p/wouldnt_it_be_nice_to_have_building_interiors_in/ — r/CitiesSkylines, "Wouldn't it be nice to have building interiors" (interiors "don't align with the primary purpose"; immense labor)

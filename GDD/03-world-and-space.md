# 03 — World, Space & Buildings

> The "big open space, but in 2D" pillar made concrete: how the world is laid out, how big
> it is, what stands on it, and how the player moves through and into it.

## 3.1 The tile world

- The world is a **2D tile grid** with **integer coordinates centred on the origin**. A tile
  is the atomic unit of space; everything spatial is expressed in tiles.
- **`TilePosition` is authoritative**; any continuous render `Location` is *derived* from it.
  This keeps the simulation deterministic and the state serialisable.
- **Default map: 256×256 tiles.** The world is generated **deterministically** — the same
  seed and options always produce the same town — so persistence, replay and tests are exact.
- **Terrain is autotiled** (a terrain manifest tracks which autotile blocks are complete);
  the terrain is the *canvas*, and buildings, roads and networks are the *content* on it.

## 3.2 Roads, lots and buildings

- The map's road network creates **lots**. A lot's interior is `(RoadSpacing − 1)²` tiles; the
  default `RoadSpacing = 12` gives an **11×11 lot**.
- A generated building is anchored at its lot's top-left corner, so in generated worlds there
  is **one building per lot**, and the **lot count is the building ceiling**.
- **Building kinds:** Residential, Office, School, Store, and Civic/Shelter. Each has a
  validated footprint placed via an occupancy/placement service, with an admin placement
  console for hand-authored worlds.
- **Scaling law.** If a footprint grows by a linear factor *N*, the lot pitch must grow by
  *N*, so the lots per axis fall by *N* and total lots by *N²*; holding the same building
  count therefore requires the map's **linear size to grow by N** (area by N²). This is why
  the world-size and building-sizing work are one problem, not two.

## 3.3 The world-size question (the "open space" reality)

The default 256×256 world is already **too small for the intended population** and for
interiors:

- The API ceiling (~5,000 genoses ⇒ ~411 buildings) exceeds the 256×256 default's 400 lots.
- **Properly sized buildings** are a prerequisite for interiors — a building must have a
  footprint big enough to hold a real interior room, which multiplies footprint area by
  roughly **3.5×–22×** depending on kind.

Direction of travel (research landed, decision pending):
- Grow the world toward a **much larger** target; research and tooling exist for a
  **32,000×32,000** world (migration surface + streaming discipline), with a **pure-function
  map generation** refactor already landed so that terrain is a function of coordinate rather
  than of a mutable stream.
- Resize footprints to the **interior minimum viable** size first (a readable 2×2-tile room,
  with circulation and common areas), deferring "comfortable" footprints to a later version.
- **Ship footprint change and road-spacing change atomically** — a footprint that fits no lot
  makes generation *throw*, not degrade, so the two must land together.

## 3.4 Interiors (the personal-space pillar)

Interiors are how a building stops being a symbol and becomes a place. Decided positions:

- **Player-only first.** Interiors refine presence for *the player's* genos; NPCs keep
  macro **footprint-presence** (no per-NPC room simulation — the performance ruling forbids
  it). NPC interior access is a later, performance-gated feature.
- **Cutaway roof** as V1; **multilevel** cutaway as V2.
- **Generic per building kind** on the renderer-prototype; **procedural** (spritesheets /
  tilesets) on the desktop client. The *model* is authored per `BuildingKind` and *derived*
  per placed building — an `Interior` derived from an authored `InteriorTemplate`, never
  serialised, exactly like the world map.
- **Private interiors first; shared interiors as V2.**
- **Replication discipline:** interiors are a **scoped request**, never a per-building map
  field — the client requests the interior plan of the single building it is inside, mirrors
  the map-snapshot pattern, and caches it until exit. Room occupancy rides the existing world
  delta as a per-genos `InteriorLocation` (which must be added to `GenosesEqual` or it will
  silently never replicate).

## 3.5 Movement and presence

- The player moves by **server-authoritative** movement driven by typed intents; genoses
  commute and are visible near the player via **area-of-interest / viewport filtering**.
- Presence is **footprint-level for everyone** and **room-level for the player** (see §3.4).
- No client-side prediction is shipped today — the measured latency made it a no-go; the
  client interpolates between server states instead. This is a rendering concern for the
  desktop client, not a model concern.

## 3.6 What the world must feel like

- **Open and traversable:** the player can wander a real town, not a level.
- **Legible:** the player can tell a house from an office from a school from a shop *by
  looking*, and can find their own home and workplace.
- **Consequential:** where you are matters — rent, commute, whom you meet, what you can buy.
- **Scalable:** the world is designed to grow in size *and* in building interiority without
  changing the model — the durable artifact is the tile/lot/building/interior contract.

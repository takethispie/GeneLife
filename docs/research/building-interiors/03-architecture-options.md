# 03 — Architecture options

**Verified against:** `master` @ `dc5f2ce`. This file states the real options and the trade they make; the choice is
made in [07](07-recommendation.md). Prior-art precedents are cited to [02](02-prior-art.md).

## The two axes that define every option

Every candidate is a point on **two independent axes**, and conflating them is the main source of confusion when
this subject comes up:

- **Axis 1 — Where the space lives.** Is the interior *more of the world map* (an overlay on the building's existing
  footprint, the RimWorld / Prison Architect / Project Zomboid model — [02](02-prior-art.md)) or a *separate map*
  reached by a transition (the Sims / instanced-MMO model — [02](02-prior-art.md))?
- **Axis 2 — Where the layout comes from.** Is each building's interior **authored/parameterised from a shared
  template per `BuildingKind`** (five templates — one each for Residential, Office, School, Store, Civic —
  `BuildingKind.cs`), or **generated per building** from that building's own data (`UnitCount`, `Capacity`)?

On top of both sits the constant from [01 §3](01-problem-and-current-state.md): **the macro simulation keeps
footprint-presence regardless of option** — the interior is a refinement for the player's scope, never a new
per-NPC simulation. That constant is not negotiable under the 2026-11-27 performance ruling.

---

## Option A — Separate interior map, per-building instance, with a transition

**Space model.** The interior is its own small tile map (a room grid), owned by the server and attached to a
`NetworkId`. Entering a building loads that map; leaving unloads it.

**Transition.** A scene swap on the client: the genos leaves the world view and the client renders the interior map
(the Sims lot-loading pattern, [02] refs 7–9; the instanced-MMO pattern, refs 16–18).

**Data / scope.** The server sends the interior map only for the building the player is in (plus, optionally, the
occupants). Payload is bounded by *one building at a time*, which is the MMO justification for the whole pattern
([02] ref 16).

**Cost.** Highest up-front: a second map type, a second replication path, a transition state machine on both server
and client, and an "am I inside / which interior" connection state that has to survive reconnects.

**Risk.** The transition is a **seam the player sees**, and seams are where bugs and bad UX live (desync between the
world tick and the interior, a reconnect that drops the interior state, a second player in the same building). It
also duplicates map infrastructure (a second `WorldMap`-like index) the codebase would have to keep in sync.

**What it forecloses.** A seamless feel: you cannot see the street from inside, and the roof-cutaway "peek" that a
same-map model allows is off the table. It *also* forecloses the cheapest failure mode (see Option B's risk below) —
with a separate map the interior always works, it just costs more.

**Prior-art verdict.** This is the *proven* model for exactly GeneLife's constraint (many buildings, bounded
payload) — but it was proven in a **3D client where the loading screen is cheap** and the interiors are the payoff
for expensive art. GeneLife is a 2D tile client whose interiors would start generic.

---

## Option B — Sub-grid / overlay on the existing map (no transition)

**Space model.** The interior is an **index over the building's own footprint tiles** — a room graph whose nodes are
rectangles inside the existing `Footprint` (`Placement.cs:7-29`), exactly as a Project Zomboid `RoomDef` is a
rectangle with an `isInside` test over one continuous map ([02] refs 13–15). No second map.

**Transition.** None. Entering hides the roof (a cutaway, the standard top-down device) and the camera zooms to the
building; the world keeps rendering around it. This is the Dwarf-Fortress "more of the same grid" feel ([02] refs
10–12) and the PZ model.

**Data / scope.** A room is *data about tiles the client already has*: the map snapshot already ships each building's
`X, Y, Width, Height` (`MapStatus.cs:46-58`, `MapSnapshot.cs`). A room plan can therefore be small — the minimum is
one `Inside` boolean per tile plus a room id, exactly Prison Architect's save shape ([02] ref 6).

**Cost.** Lowest up-front. No second map, no transition state machine, no second replication path — the interior is
a new *view* of existing geometry. It reuses the occupancy model `WorldMap` already maintains
(`WorldMap.cs:59-64,89-95`).

**Risk.** The footprint is an **envelope sized to content, not a floor plan** — a 40-unit property is an 8×5
rectangle (`BuildingFootprints.cs:6-15`). An overlay that simply reveals those tiles shows a **featureless open
box**, not rooms. So Option B only works if it is paired with a layout source (Axis 2) that subdivides the
envelope into rooms — i.e. Option B *does not solve the room model by itself*; it only solves the transition and the
scope.

**What it forecloses.** The "separate space" fantasy: everything happens on one map, so an interior cannot be bigger
than, or shaped unlike, its footprint, and cannot be a distinct instance two players enter independently. It also
forecloses the "home as a private instanced space" pattern the MMO family sells ([02] refs 16–18).

**Prior-art verdict.** PZ and Prison Architect ship this; it is the **cheapest correct** model for a continuous
overworld client, which is what the browser client is.

---

## Option C — Instanced interiors: one authored template per `BuildingKind`, instantiated per building

This is **Axis 2 alone** — it can be layered onto either A or B, and it is the option that actually answers "where do
rooms come from".

**Space model.** Five **authored, parameterised templates** — one per `BuildingKind` (`BuildingKind.cs`) — each with
named rooms and anchor slots. A given building's interior is its template *instantiated* with that building's data:
a Residential template's rooms are bound to `HousingUnit`s (`HousingUnit.cs:12-24`, which already carry a
`UnitNumber` and an `Owner`), an Office template's desks to `Office.Capacity`/`SeatsUsed`
(`Genelife.Domain/Components/Company/Office.cs`), a School template's rooms to `School.Capacity`/`Enrolled`.

**Transition.** Whatever the host option (A or B) provides.

**Data / scope.** The **template is code/content** (authored once, five times); the **per-building instance is tiny**
— a building id plus the bindings to its existing domain containers. This is what makes "thousands of buildings"
tractable: the *plan* is one of five, and the *instance* is a lookup ([05](05-contract-and-server.md)).

**Cost.** Low-to-moderate, and mostly content work: authoring five floor plans, and a binding layer that maps each
template slot to the building's real sub-entities.

**Risk.** Authored templates can look repetitive (every house is the same house). The mitigation is
parameterisation (unit count changes the plan) and, later, per-district variants — but the *first* version will be
visibly generic, and that is a product expectation to set now ([08](08-open-questions-for-the-po.md)).

**What it forecloses.** Fully emergent, per-building-unique interiors. It commits GeneLife to "interiors are set
dressing that make the sub-entities visible", which is a defensible and *cheap* position, but it is a position.

**Prior-art verdict.** This is how the domain *already thinks*: a `Property` has `UnitCount` units and each
`HousingUnit` has a `UnitNumber` — the sub-spaces exist in the data and only lack coordinates
([01 §2.5](01-problem-and-current-state.md)). Option C is the smallest change that gives them a place.

---

## Option D — Hybrid: same-map overlay for the render, scoped instanced payload for the data, footprint for the sim

**Space model.** Not a fourth geometry — a *layering* of the previous three along the two axes:

- **Sim / macro:** footprint-presence, unchanged, for every NPC and for occupancy
  (`SimulationEngine.cs:6520-6533`). Constant across all options.
- **Data:** Option C — a per-`BuildingKind` template instantiated by building id, delivered as a **scoped** payload
  ([05](05-contract-and-server.md)).
- **Render:** Option B — an overlay/cutaway on the same map, no transition, implemented first in whichever client
  renders the world.

**Transition.** None at the boundary (the roof lifts); the "scope" changes on the server when the player's genos
enters.

**Cost.** The sum of the *render* half of B and the *data* half of C — but **neither* the second map of A nor a
per-NPC simulation.

**Risk.** The real risk is **scope creep into Option A** ("while we're here, let's instance it"), and the risk that
the overlay does not read as "inside" without art investment. Both are managed by freezing the seam early
([07](07-recommendation.md)).

**What it forecloses.** Same as B (no separate space) — but it *keeps open* the upgrade path to A, because the
*template + binding* model of C is exactly what an instanced interior needs; only the renderer would change.

---

## Comparison

| | A — Separate map + transition | B — Same-map overlay | C — Instanced templates (data) | **D — Hybrid (C data + B render)** |
|---|---|---|---|---|
| Second map / second replication path | **Yes** (doubles map infra) | No | No | **No** |
| Transition seam the player sees | **Yes** (loading/swap) | No (roof cutaway) | — (host-dependent) | **No** |
| Solves "where do rooms come from" | Partial (still needs C) | **No** — needs C | **Yes** | **Yes** |
| Payload bounded per building | **Yes** (strongest) | Yes (rooms are tile data) | **Yes** | **Yes** |
| Sim cost (per-NPC) | unchanged if scoped | unchanged if scoped | unchanged | **unchanged** |
| Up-front cost | High | Low | Low–moderate (content) | **Low–moderate** |
| Content risk | High (art in a 3D-style seam) | Medium (open-box read) | **Medium (repetition)** | Medium |
| Upgrade path to instanced space later | — (already there) | **Closed** (would be a rewrite of the seam) | open | **Open** (C already fits A) |
| Fits a *continuous* 2D overworld client | Poorly | **Well** | Neutral | **Well** |
| Prior-art precedent | Sims, WoW/FFXIV/ESO | PZ, Prison Architect, DF | (RimWorld rooms are the reverse: derived) | PZ + a template index |

## Which axis each option actually decides

- The **render/transition** question (A vs B) is the *client-visible* one and the one the PO's "display" wording
  points at. It is also the **only** one that is client-specific, and therefore the only one a client choice can
  waste ([06](06-client-implications.md)).
- The **layout** question (C) and the **scope** question (A/B) are **client-agnostic** and live in the server and the
  contract. They are the durable artifact ([07](07-recommendation.md)).

**Conclusion of this file.** Option D is the only option that (a) keeps the *entire* costly decision — the room
model and the payload scope (C) — on the server side of the wire, (b) adds no second map, and (c) leaves the
instanced-space upgrade path open. Options A and C are not discarded; A is the *upgrade*, and C is a *component* of
D. The recommendation in [07](07-recommendation.md) is D.

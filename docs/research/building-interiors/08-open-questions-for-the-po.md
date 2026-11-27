# 08 — Open questions for the PO — ANSWERED (2026-11-27)

**Status: answered.** All five questions below were ruled on by the PO on **2026-11-27**. This file is no longer an
open menu: each question now carries a **`Ruling (PO, 2026-11-27)`** block that records the decision and what it
commits the project to. The old *"What the dossier assumes"* paragraphs are gone — nothing here is left standing as
if it were still open. (Only genuine **product** decisions ever lived here — choices the recommendation cannot make
for the product owner.)

**Rulings index:** Q1 player-only interiors · Q2 generic-per-kind on web / procedural on Godot · Q3 roof cutaway
(V1) → multilevel (V2), with a footprint-and-world-size consequence · Q4 the browser client is a stepping stone ·
Q5 private interiors first, shared as a V2.

Two rulings change more than the question they answer: **Q4** re-frames (but does not change) the conclusion in
[07](07-recommendation.md), and **Q3** adds a named planning item there. Both are called out below.

---

## Q1 — Are interiors wanted at all, and for whom?

**Question:** Should the player be able to *be inside* a building and see a room — or is a building a place you
arrive at, with presence (work / home / shop) shown as a status, and no inside at all? And if there is an inside, is
it for the **player's genos only**, or must NPCs visibly occupy rooms too?

**Trade-off.** Interiors for the player only are cheap and make the existing entry flow meaningful
([06 §6](06-client-implications.md)). Interiors for every NPC are a per-tick simulation of rooms for thousands of
genoses — the performance ruling of 2026-11-27 forbids spending the tick budget there before Godot parity
([01 §3](01-problem-and-current-state.md)), so "NPCs too" is effectively a *different* (later, performance-gated)
feature. Choosing "no interiors at all" is legitimate — it is Cities: Skylines' position ([02](02-prior-art.md)) —
but then the entry flow should be *designed to end at the door* rather than imply a room.

**Ruling (PO, 2026-11-27):** Interiors **are wanted**. They are **player-only at first** — only the player's genos
can be inside and see a room. **NPCs gain interior access later**: NPC rooms are a **later, performance-gated**
feature, **not v1**. This is the option the dossier's own trade-off named as the cheap, correct one.

**What this commits the project to:** the first interior slice is scoped to the **player's genos**, reusing the
sub-spaces the domain already counts (`UnitCount`, `Capacity`). It does **not** commit v1 to a per-NPC room
simulation, and the constant from [03](03-architecture-options.md) holds: the macro sim keeps **footprint-presence**
for every NPC regardless of option. A future "NPCs inside" feature is a separate, performance-gated card, not a
widening of this one.

---

## Q2 — How generic may v1 interiors be?

**Question:** Is it acceptable that the first interiors are **generic per building kind** — every residential block
subdivided the same way, every office floor the same — with per-building variation limited to genuinely varying
things (number of units, capacity)? Or does the product need visibly distinct interiors from day one?

**Trade-off.** Generic templates are the cheap, testable, ship-now option and reuse the sub-spaces the domain
already counts (`UnitCount`, `Capacity` — [04 §2](04-data-and-sim-model.md)). Distinct interiors mean procedural
generation or an authored set per district — a real content-and-systems bill with a readability and testability
cost. This is the classic "every shop looks the same" trade the template approach accepts
([02](02-prior-art.md), Option C).

**Ruling (PO, 2026-11-27):** The two clients diverge **on purpose**. On the **web client** interiors are
**generic per building kind** — every building of a kind subdivides "the same way" (the cheap, template option). On
**Godot** interiors are **procedural**, rendered with **spritesheets / tilesets** (generated per building, drawn
from a tileset rather than one authored plan per kind).

**What this commits the project to — a recorded divergence, not a collapse.** The generic web renderer and the
procedural Godot renderer are **two different implementations behind the same model and the same contract**
(Option C — `InteriorTemplate`, `Room`, and the bindings — [03](03-architecture-options.md), [04](04-data-and-sim-model.md),
[05](05-contract-and-server.md)). Two consequences the ruling forces:

1. The **generic renderer is v1's only renderer** and satisfies the "generic is acceptable for v1" position, with
   parameterised variation by `UnitCount` / `Capacity`.
2. **Procedural generation is Godot's job.** The web slice must **not** grow a procedural generator — it stays a
   template renderer. Building a procedural generator in the browser would be exactly the scope creep that a
   stepping-stone client (Q4) is the wrong place to pay for.

---

## Q3 — Separate space with a transition, or the same map with a cutaway?

**Question:** When the genos goes in, should the game **cut away the roof and keep the town visible around the
building** (no loading screen), or should it **load the interior as its own space** (a short transition, the house
becomes your own instance)?

**Trade-off.** The cutaway is the cheap, seamless, single-map model (Project Zomboid / Dwarf Fortress feel) and adds
no second map — but the interior can be no bigger or stranger than the building's footprint
([03](03-architecture-options.md), Option B). The instanced interior (Sims / MMO feel) lets interiors be their own
spaces and supports private homes and bigger-than-footprint rooms, at the cost of a second map, a transition seam
the player sees, and duplicated map infrastructure (Option A). The recommendation starts at the cutaway and keeps
the instanced model as a documented upgrade ([07](07-recommendation.md), risk 1).

**Ruling (PO, 2026-11-27):** **Roof cutaway as V1**, **multilevel roof cutaway as V2.** This confirms Option D
(same-map overlay, cutaway render) as the v1 model and pushes the "separate instanced space" fantasy past v1. The PO
attached an explicit **consequence**: buildings must be **properly sized now** — footprints big enough to hold a real
interior — and the **world must be bigger**.

**What this commits the project to:** the cutaway is not just a renderer choice — it forces a **footprint-size and
world-size** change to the map. Because the cutaway interior can be no larger than the building's footprint
(Option B's stated risk, [03](03-architecture-options.md)), the existing footprints — sized to content, not to a
floor plan ([04 §5](04-data-and-sim-model.md)) — are now a **blocker to be scoped**, not a footnote. This is
recorded as a **named planning item** in [07](07-recommendation.md#named-planning-item--footprint-sizing-and-world-size-q3-consequence),
which is **its own scoping card** and is **not** part of the interior-research card. Multilevel cutaway is a named
**V2** and needs no v1 work.

---

## Q4 — Is the browser client a long-term ship, or a stepping stone to the Godot client?

**Question:** When the Godot desktop client exists, does the **browser client keep shipping features**, or does it
become the fallback/reference and stop receiving new gameplay? Concretely: is the browser client expected to
receive interiors *as a permanent feature*, or is it the quickest way to prove the model before the real client
takes over?

**Trade-off.** This is the one decision that can actually change the sequencing answer ([07](07-recommendation.md),
falsifier 2). If the browser keeps shipping, a browser-first renderer is a permanent asset and the recommendation
stands as-is. If the browser is a stepping stone, the first renderer is explicitly a **prototype-only** slice whose
sprite code is expected to be retired — the model and contract are unaffected either way
([06 §4](06-client-implications.md)), but the *effort* justified in the browser changes.

**Ruling (PO, 2026-11-27):** The browser game client is a **stepping stone**, **not a long-term ship**. This is
**falsifier 2** from [07](07-recommendation.md)'s own *"What evidence would change the answer"* list, so the
recommendation's framing is updated there — the conclusion survives, the framing does not.

**What this commits the project to:**

- The browser renderer is a **prototype-only slice whose purpose is to validate the model and the UX**, and whose
  **sprite code is expected to be retired** when the Godot client takes over.
- The **"do not defer the research"** and **"decide the model and the contract now, server-first"** halves of the
  recommendation **survive unchanged**: the durable artifact is the **model and the contract**, and neither depends
  on which client renders first.
- The **real renderer lands in Godot.** Browser work is no longer framed as a permanent asset.

See [07 — Update after the PO's ruling](07-recommendation.md#update-after-the-pos-ruling-2026-11-27).

---

## Q5 — May two players share an interior?

**Question:** In the multiplayer world, if two players' genoses are in the same building, must each see the other
inside the room? Or is an interior a private view of the building for whoever is in it?

**Trade-off.** Shared interiors are the natural extension of the existing server-authoritative multiplayer model
(other genoses already replicate with `InteriorLocation` riding the viewport-filtered delta —
[05 §5](05-contract-and-server.md)), but they raise real questions the model does not answer for free: do occupancy
limits apply, do players collide, can one player see another's home? Private interiors are simpler and match the
instanced pattern ([02](02-prior-art.md)); shared interiors match the "always-online world" the rest of the game
already assumes. This decision can be deferred past the first slice, but it should be named before interiors are
declared shipped.

**Ruling (PO, 2026-11-27):** **Private interiors first**; **shared as a V2.** In v1 an interior is a **private
view** of the building for whoever is inside it — another player's genos is **not** rendered inside the same room —
and shared interiors where players see each other inside move to a named **v2**.

**What this commits the project to:** v1 renders the interior as a **private view**, so the first slice does **not**
need the shared-room replication path (`InteriorLocation` on the viewport-filtered delta, [05 §5](05-contract-and-server.md))
to be wired for *inside-the-room* visibility. That machinery is not deleted — the model must keep the v2 upgrade
open — but it is not a v1 requirement. Shared interiors stay a named v2 feature.

> **Read as given, flagged:** the PO's Q5 answer specifies the private-first / shared-v2 split but does not further
> define what "private" means for a building that already has occupants (e.g. whether an owner's own unit is
> visible to them while another player is in a different room of the same building). This dossier records the ruling
> as *"an interior is a private view for whoever is inside it; players do not see each other inside in v1"* and
> marks the finer occupancy semantics as not specified — to be settled when a v2 shared-interiors card is picked up,
> not invented here.

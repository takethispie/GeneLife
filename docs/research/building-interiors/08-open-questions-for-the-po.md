# 08 — Open questions for the PO

Only genuine **product** decisions live here — choices the recommendation cannot make for the product owner. Each is
a question with its trade-off, and a note on what the rest of this dossier *assumes* until it is answered.

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

**What the dossier assumes:** player-scoped interiors are wanted; the PO will confirm or veto before an
implementation card is created.

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

**What the dossier assumes:** generic is acceptable for v1, with parameterised variation and a later per-district
pass.

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

**What the dossier assumes:** start with the cutaway (Option D); the PO decides whether the "private instanced
home" fantasy is a requirement that pulls Option A forward.

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

**What the dossier assumes:** the browser keeps shipping for now (no repo record says otherwise; V3/V5 still call
it "the game client"), so browser-first is framed as a real implementation, not a throwaway.

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

**What the dossier assumes:** interiors are visible to whoever is in the building (server-authoritative, like the
rest of the world); the PO may scope them to private instead.

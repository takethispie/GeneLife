# 07 — Recommendation

**Verified against:** `master` @ `dc5f2ce`. This file answers the PO's actual question and satisfies acceptance
criteria 2 and 3. **PO rulings applied:** 2026-11-27 (see the update directly below).

## Update after the PO's ruling (2026-11-27)

The PO answered all five open questions in [08](08-open-questions-for-the-po.md). One answer — **Q4: the browser
client is a stepping stone** — is **falsifier 2** from
[§What evidence would change the answer](#what-evidence-would-change-the-answer), so it is applied here rather than
left as a menu. **The conclusion does not change; the framing does.** The pre-ruling text is kept in full below,
annotated inline, so a reader can see what the recommendation said before the ruling and why the ruling moved the
framing and not the conclusion.

- **The browser renderer is a prototype-only slice whose purpose is to validate the model and the UX.** Its
  **sprite code is expected to be retired** when the Godot client takes over — it is scaffolding, not a product
  feature. This changes the *sequencing* framing only; it changes nothing in the architecture.
- **The two halves of the recommendation survive unchanged.** *"Do not defer the research"* and *"decide the model
  and the contract now, server-first"* are untouched by Q4: the durable artifact is the **model and the contract**
  (the `Genelife.Contracts` surface, the room model, the scoped payload, the entry authority), and **neither depends
  on which client renders first**. Q4 says where the *first* renderer goes and what it is for; it does not touch a
  single server-side decision.
- **The real renderer lands in Godot** — and by **Q2** it is the **procedural** one (spritesheets / tilesets), while
  the browser slice is the generic template renderer.
- **What is still true, unchanged:** the research is still not deferred; the model and contract are still decided
  now; the browser is still first *to render* (it is the only client that exists); and the browser still has no
  equivalent of the live entry flow in a Godot it could defer to.

**Bottom line, unchanged:** *the model is the deliverable, the renderer is disposable.* The ruling makes the
"disposable" half explicit where it used to be an inference.

## The recommendation, stated plainly *(pre-ruling wording, annotated)*

1. **Do not defer the research.** *(Confirms the Tech Leader's position, with evidence.)*
2. **Do not defer the model or the contract.** Decide the space model and the snapshot scope now, and build them
   **server-first** — they are client-agnostic ([04](04-data-and-sim-model.md), [05](05-contract-and-server.md)).
3. **Implement the first renderer in the browser client** — *not* in a throwaway prototype, and *not* deferred to
   Godot — as a **thin vertical slice** (enter → request plan → draw one room → exit) against the contract.
   > **Post-ruling (Q4):** this is the sentence the ruling changed. The browser slice is now a **prototype-only
   > slice** whose purpose is to validate the model and the UX; its sprite code is **expected to be retired**. The
   > browser client is a **stepping stone**, not a permanent asset. "Not in a throwaway prototype" now reads
   > *relative to the model*: the slice is real enough to exercise the contract, but it is disposable relative to
   > the product.
4. **The Godot client implements the identical contract** when it exists. Its parity bar is set by whatever the
   browser ships first.
   > **Post-ruling (Q4 + Q2):** the **real renderer lands in Godot**, and it is the **procedural** one
   > (spritesheets / tilesets, generated per building — [08 Q2](08-open-questions-for-the-po.md)). The browser's
   > generic template renderer is a validation slice, not the permanent target of the parity bar.

One line: **"Web-first renderer on a server-first contract; the model is the deliverable, the renderer is
disposable."**
> **Post-ruling:** still exactly true, and now literally so for the browser renderer. The contract-first,
> model-is-the-deliverable half is **unchanged**; only the browser renderer's tenure changes.

## Answering the PO's question, directly

> should this research be deferred to after making the godot desktop client or is the web game client not far
> enough and so this would still be in the scope that web client?

- **The research: not deferred.** The three expensive sub-problems (space model, payload scope, entry authority)
  are the same whichever client draws them, and two of them are already half-decided in the repository
  ([01](01-problem-and-current-state.md)). Deferring them defers *decisions on the server*, which no client choice
  can justify.
- **The first implementation: the browser client.** It is the only client that exists ([06 §1](06-client-implications.md)),
  it already ships the entry flow with nowhere to go (`building-selection.ts`, [06 §6](06-client-implications.md)),
  and the PO's own 2026-11-27 ruling makes the browser the **reference** Godot must reach parity with. Building the
  first renderer anywhere else would either block on a client that does not exist or duplicate the renderer before
  the model is proven.
  > **Post-ruling (Q4):** the browser is still first *to render* — it is the only client that exists — but the
  > ruling reframes it from a permanent reference to **a stepping stone**: a prototype-only slice to validate the
  > model and the UX, whose sprite code is expected to be retired. The *parity* wording above comes from the
  > 2026-11-27 performance ruling and still holds for the *game client as a whole*; Q4 governs the **interior
  > renderer**, which is disposable.

## The reasoning, in order of weight

### R1 — The repository's own record makes the browser the reference, and says so on the parity bar

- V3 and V5 both **scoped Godot out** in explicit language: *"No Godot, no native desktop shell, no engine
  rewrite"* (`docs/plans/roadmap-v3/README.md:3`, non-goals `:55`), *"No Godot, native desktop shell or engine
  rewrite. The browser client remains the game client."* (`docs/plans/roadmap-v5/README.md:3,50`). `ROADMAPS.md`
  repeats it (`:11`).
- The 2026-11-27 product-owner ruling recorded in `docs/research/post-perf-feature-evolution.md:11-14,349-350`
  changes Godot's **status** — it is now a declared direction with a documented parity bar — but it does not create
  Godot code. There is still no Godot project in the tree ([06 §1](06-client-implications.md)).
- **So what changed and when:** between V5 and 2026-11-27, Godot moved from "scoped out" to "declared direction
  with a parity requirement", while remaining non-existent. The browser went from "the game client" to "the
  reference client Godot must match". The sequencing argument therefore rests on the repo's own record: **the
  browser is the incumbent, Godot is the aspirant, and the parity bar points the same way the recommendation
  does.**

### R2 — The entry flow is live and has nowhere to go; this is a defect the work fixes in both clients

`BuildingSelectionController` walks the genos to the door and submits `useBuilding`
(`building-selection.ts:93-180`); the server flips `CurrentAction` and stops (`SimulationEngine.cs:1460-1517`).
Nothing renders an inside. Deferring interiors leaves that state live for the whole Godot build-out
([06 §6](06-client-implications.md)).

### R3 — The durable half is server-side, so the client choice cannot waste it

The room model, the template content, `InteriorLocation`, `InteriorPlan`, the entry authority and the hub method all
live in `Genelife.Contracts` / the server ([05](05-contract-and-server.md)). A client switch cannot touch them.
This is the distinction the PO's question needs ([06 §2](06-client-implications.md)):

| Durable artifact (the model and the contract) | Replaceable renderer |
|---|---|
| `InteriorTemplate` (5), `Room`, room graph | Pixi room sprites / roof cutaway |
| `InteriorLocation` on `GenosStatus` (+ `GenosesEqual`) | DOM interior panel |
| `InteriorPlan` DTO + `RequestInteriorPlan` scope | camera zoom-to-building |
| Entry authority reused from `ApplyUseBuildingIntent` | that client's visual tests |

### R4 — The scoped payload is what makes it affordable now (and the numbers rule a per-building payload out)

A per-building interior on the map payload is ~2.6 MB at 5,000 buildings **per connect**, versus ~0.9 MB for the
building list that already ships ([05 §4](05-contract-and-server.md)). The scoped design keeps the cost at *one
building's* plan per client. This is a **constraint that rules an option out**, and it is recorded in memanto as
such.

### R5 — The prior art prefers no seam for a continuous 2D overworld

The one-map family (RimWorld, Prison Architect, Project Zomboid, Dwarf Fortress) proves a continuous interior with a
roof cutaway and no loading screen, which is what a 2D tile client can do cheaply. The instanced family (Sims,
WoW/FFXIV/ESO) proves the *scoping* but pays a transition seam GeneLife does not need yet
([02](02-prior-art.md), [03](03-architecture-options.md)). Start at the cheap end; the model (Option C) keeps the
instanced upgrade open.

## Why not the alternatives

- **Not "defer":** defers *server* decisions to a client that does not exist, and leaves a live defect live
  (R2). The Tech Leader's position is correct.
- **Not "prototype-only":** a throwaway renderer would discard nothing important, but "prototype-only" would also
  imply *not committing to the model* — and the model is the cheap, durable, must-decide-now part (R3). A thin
  **vertical slice** (a real, shipping-quality renderer for one building kind) is the right weight: it exercises
  the contract for real and can grow, unlike a prototype.
  > **Post-ruling (Q4):** this objection was about the **model**, not the renderer, and it stands — the ruling
  > commits the model and the contract (items 1–2) and does **not** make the model throwaway. What Q4 changes is
  > the **browser renderer**, which is now prototype-only. So "vertical slice" for the *contract* and
  > "prototype-only" for the *browser sprite code* are no longer in tension: the browser slice still exercises the
  > contract for real, it simply is not a permanent asset.
- **Not "Godot-first":** blocks on a nonexistent client and inverts the parity ruling ([06 §1](06-client-implications.md)).

## Risks, named

| Risk | Likelihood | Mitigation |
|---|---|---|
| **Scope creep into full instanced interiors (Option A)** — a second map, a transition state machine | High | Freeze the seam now: phase 1 is Option D, no second map ([03](03-architecture-options.md)) |
| **Interiors read as generic** (each house the same) | Certain in v1 | Set the expectation with the PO ([08](08-open-questions-for-the-po.md)); parameterise by `UnitCount`/`Capacity`; per-district variants later |
| **The `GenosesEqual` trap** — an `InteriorLocation` field silently never replicates | Medium (silent) | Add it to `GenosesEqual` in the same change; the reflect-over-DTO test is the guard ([05 §3.1](05-contract-and-server.md)) |
| **The overlay does not read as "inside"** without art investment | Medium | Budget one art pass; the footprint envelope is an open box without rooms ([04 §5](04-data-and-sim-model.md)) |
| **Browser client retired soon**, wasting the renderer | **Realized — Q4 (2026-11-27)** | The PO ruled the browser a stepping stone. Server/contract work is unaffected and only sprite code is wasted, so the mitigation holds: the browser slice is now *designed* prototype-only ([06 §4](06-client-implications.md)) |

## What evidence would change the answer

State these falsifiers up front, so the recommendation is a decision and not a preference:

1. **The Godot client is started and reaches parity before the browser next ships a feature.** Then the first
   renderer belongs in Godot; the *research and contract* recommendation is unchanged. *(No Godot project exists
   today — [06 §1](06-client-implications.md) — so this is not the current situation.)*
2. **The PO declares the browser client frozen / a stepping stone to be retired.** Then "web-first renderer" costs
   even the sprite work; switch to a prototype-only slice in the browser purely to validate UX, and land the real
   renderer in Godot. **→ MATERIALIZED (2026-11-27):** the PO ruled the browser client a **stepping stone**, so
   this is now the applied framing — see [§Update after the PO's ruling](#update-after-the-pos-ruling-2026-11-27).
   The conclusion is unchanged. *(Answered → [08 Q4](08-open-questions-for-the-po.md).)*
3. **The PO rules that interiors are out of product scope** (the Cities: Skylines position, [02](02-prior-art.md)).
   Then this dossier becomes a recorded non-decision and the entry flow should instead be *designed to end at the
   door* (e.g., an on-map "you are at work" state) rather than implying a room. **→ NOT MATERIALIZED (2026-11-27):**
   the PO ruled interiors **are wanted** (player-only at first). *(Answered → [08 Q1](08-open-questions-for-the-po.md).)*
4. **A measured constraint rules out the scoped payload** (e.g., the scoped `RequestInteriorPlan` proves
   unworkable under the 40 ms broadcast, `SnapshotBroadcastService.cs:28`). Then re-open [03](03-architecture-options.md);
   the model (Option C) survives but the delivery changes.

## Named planning item — footprint sizing and world size (Q3 consequence)

**Q3's ruling is not only a renderer choice.** The PO ruled **roof cutaway as V1** — a same-map interior, which by
construction can be **no larger than the building's footprint** ([03](03-architecture-options.md), Option B's risk)
— and attached the consequence directly: **buildings must be properly sized now** (footprints big enough to hold a
real interior) and the **world must be bigger**. This is a named planning item, not a footnote.

**Scope it implies — two changes, in order:**

1. **Map generation — a bigger world.** World size is a generator input. If interiors need buildings that hold real
   rooms, the world the generator emits has to grow to make room for the larger footprints.
2. **The existing building footprints.** Today's footprints are sized to *content*, not to a floor plan — a 40-unit
   property is an 8×5 rectangle, a **featureless open box** rather than rooms ([04 §5](04-data-and-sim-model.md),
   [03](03-architecture-options.md) Option B). A cutaway interior that fits inside them has nothing to show, so the
   footprints must be re-derived to hold a real room graph before the renderer has space to draw.

**Why it is a prerequisite, not a follow-up.** The Option-B payload assumption — *a room is data about tiles the
client already has* ([03](03-architecture-options.md)) — depends on the footprint being big enough to hold those
tiles. Sizing after the renderer exists would mean drawing rooms into footprints that cannot contain them.

**Routing.** This needs **its own scoping card**. It is **not** part of the interiors-research card
(`t_68262a29`) and **not** part of this ruling-recording card. It is a change to the **map / footprint generation**
surface, owned by whoever owns world generation, and it has prerequisites (it changes the footprint size that
placement and occupancy features assume). Name it before any interior-render card starts.

## Acceptance-criterion cross-check

- **AC2 — answers the PO's question with a firm recommendation:** yes, §"Answering the PO's question" and §"The
  recommendation". Not a menu.
- **AC3 — distinguishes durable artifact from replaceable renderer and says what is wasted:** per R3 and
  [06 §4](06-client-implications.md) — the throwaway is 2D sprite code, a DOM panel, a camera call and one client's
  visual tests; the model and the contract survive a client switch intact.

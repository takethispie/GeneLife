# 07 — Recommendation

**Verified against:** `master` @ `dc5f2ce`. This file answers the PO's actual question and satisfies acceptance
criteria 2 and 3.

## The recommendation, stated plainly

1. **Do not defer the research.** *(Confirms the Tech Leader's position, with evidence.)*
2. **Do not defer the model or the contract.** Decide the space model and the snapshot scope now, and build them
   **server-first** — they are client-agnostic ([04](04-data-and-sim-model.md), [05](05-contract-and-server.md)).
3. **Implement the first renderer in the browser client** — *not* in a throwaway prototype, and *not* deferred to
   Godot — as a **thin vertical slice** (enter → request plan → draw one room → exit) against the contract.
4. **The Godot client implements the identical contract** when it exists. Its parity bar is set by whatever the
   browser ships first.

One line: **"Web-first renderer on a server-first contract; the model is the deliverable, the renderer is
disposable."**

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
- **Not "Godot-first":** blocks on a nonexistent client and inverts the parity ruling ([06 §1](06-client-implications.md)).

## Risks, named

| Risk | Likelihood | Mitigation |
|---|---|---|
| **Scope creep into full instanced interiors (Option A)** — a second map, a transition state machine | High | Freeze the seam now: phase 1 is Option D, no second map ([03](03-architecture-options.md)) |
| **Interiors read as generic** (each house the same) | Certain in v1 | Set the expectation with the PO ([08](08-open-questions-for-the-po.md)); parameterise by `UnitCount`/`Capacity`; per-district variants later |
| **The `GenosesEqual` trap** — an `InteriorLocation` field silently never replicates | Medium (silent) | Add it to `GenosesEqual` in the same change; the reflect-over-DTO test is the guard ([05 §3.1](05-contract-and-server.md)) |
| **The overlay does not read as "inside"** without art investment | Medium | Budget one art pass; the footprint envelope is an open box without rooms ([04 §5](04-data-and-sim-model.md)) |
| **Browser client retired soon**, wasting the renderer | Low | Server/contract work is unaffected; only sprite code is wasted ([06 §4](06-client-implications.md)) |

## What evidence would change the answer

State these falsifiers up front, so the recommendation is a decision and not a preference:

1. **The Godot client is started and reaches parity before the browser next ships a feature.** Then the first
   renderer belongs in Godot; the *research and contract* recommendation is unchanged. *(No Godot project exists
   today — [06 §1](06-client-implications.md) — so this is not the current situation.)*
2. **The PO declares the browser client frozen / a stepping stone to be retired.** Then "web-first renderer" costs
   even the sprite work; switch to a prototype-only slice in the browser purely to validate UX, and land the real
   renderer in Godot. *(Open question → [08 Q4](08-open-questions-for-the-po.md).)*
3. **The PO rules that interiors are out of product scope** (the Cities: Skylines position, [02](02-prior-art.md)).
   Then this dossier becomes a recorded non-decision and the entry flow should instead be *designed to end at the
   door* (e.g., an on-map "you are at work" state) rather than implying a room. *(Open question → [08 Q1](08-open-questions-for-the-po.md).)*
4. **A measured constraint rules out the scoped payload** (e.g., the scoped `RequestInteriorPlan` proves
   unworkable under the 40 ms broadcast, `SnapshotBroadcastService.cs:28`). Then re-open [03](03-architecture-options.md);
   the model (Option C) survives but the delivery changes.

## Acceptance-criterion cross-check

- **AC2 — answers the PO's question with a firm recommendation:** yes, §"Answering the PO's question" and §"The
  recommendation". Not a menu.
- **AC3 — distinguishes durable artifact from replaceable renderer and says what is wasted:** per R3 and
  [06 §4](06-client-implications.md) — the throwaway is 2D sprite code, a DOM panel, a camera call and one client's
  visual tests; the model and the contract survive a client switch intact.

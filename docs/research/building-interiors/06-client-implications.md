# 06 — Client implications: web/Pixi vs Godot, side by side

**Verified against:** `master` @ `dc5f2ce`.

## 1. The two clients, as they actually stand

| | Browser client | Godot client |
|---|---|---|
| Exists in the tree? | **Yes** — `clients/browser` (TypeScript + Vite + PixiJS + SignalR) | **No** — there is no Godot project, scene or `.csproj` anywhere in the repo |
| Status | Live / shipping (`docs/plans/ROADMAPS.md:11,42-51`; V3 "Implemented (vertical slice)") | Declared direction only |
| Repo record | V3 scope: *"a browser-only 2D game client … No Godot"* (`roadmap-v3/README.md:3`, non-goals `:55`) | V5 scope: *"No Godot, native desktop shell or engine rewrite. The browser client remains the game client."* (`roadmap-v5/README.md:3,50`) |
| Godot's declared status | — | The 2026-11-27 ruling makes Godot the target for a performance-parity bar: perf is out of scope *"until the Godot desktop client reaches parity with the browser client"* (`docs/research/post-perf-feature-evolution.md:11-14,349-350`) |

**The single most important fact for sequencing: Godot does not exist yet.** It is a direction with a ruling
attached, not a codebase. Every statement below about "the Godot client must implement X" is therefore a statement
about a *future* client, while the browser client is a statement about *today's*.

## 2. What is shared and what is per-client

The whole point of this dossier is that the expensive half is shared:

| Concern | Shared (server + `Genelife.Contracts`) | Browser-only | Godot-only |
|---|---|---|---|
| Room model (`Room`, `RoomGraph`) | ✔ [04](04-data-and-sim-model.md) | | |
| `InteriorTemplate` content (5 templates) | ✔ data | | |
| `InteriorLocation` on `GenosStatus` | ✔ | | |
| `InteriorPlan` payload + `RequestInteriorPlan` | ✔ | | |
| Entry authority (Chebyshev ≤ 1, `ApplyUseBuildingIntent`) | ✔ | | |
| **Roof cutaway + camera zoom-to-building** | | ✔ `building-layer.ts`, `camera.ts` | ✔ |
| **Room floors/walls/furniture sprites** | | ✔ Pixi `Container`s | ✔ Godot `Node2D` |
| **The interior panel / room HUD** | | ✔ DOM HUD (`ui/hud.ts`, `building-panel.ts`) | ✔ Godot `Control` |
| **Input handling for "exit"** | | ✔ `input/` | ✔ |

Everything above the horizontal rule is **contract**; everything below is **renderer + input**, and is the only part
that is client-specific.

## 3. What each client must implement for interiors

**Shared (both, already covered by [04](04-data-and-sim-model.md)/[05](05-contract-and-server.md)):** the server
work, the DTOs, the hub method, the entry state transition. Neither client is involved.

**Browser client — must implement:**

1. A **roof toggle / cutaway**: today `BuildingLayer` draws `bodyPool`, `roofPool`, `entryPool`, `labelPool`
   (`clients/browser/src/render/map/building-layer.ts:9-60`) — the roof is a sprite that would be hidden while the
   player is inside.
2. A **room renderer**: draw `Room.Bounds` as floors, walls on the room edges, and any fixtures, in local interior
   space.
3. **Camera/zoom** onto the entered building (the camera already exists, `render/camera.ts`).
4. An **interior panel** to replace/augment the current building panel, which today shows only `name / kind ·
   district · w×h @ (x,y) · entry (x,y)` and an owner id (`clients/browser/src/ui/building-panel.ts:14-30`).
5. **Request the plan** on entry (`RequestInteriorPlan`), cache it, drop it on exit.
6. **Input**: an explicit exit affordance, and the existing `cancelPendingInteract`-style cancellation semantics for
   movement while inside (`building-selection.ts:114-125`).

**Godot client — must implement:** the same six items, in Godot's idioms (`Node2D` rooms, `Control` HUD, camera
zoom, `request_interior_plan`), **against the identical contract**. Nothing about items 1–6 tells the server
anything new.

**Verification split:** the *contract* is verifiable without either client — server unit/integration tests and the
browser's fixture/Vitest/Playwright tiers already exist (`docs/plans/ROADMAPS.md:49-51`; the four CI checks are
`unit, integration, client, admin-e2e`, `.github/workflows/ci.yml`). The *renderer* is verifiable per client only.

## 4. What is thrown away if the web client implements it first

Answering acceptance criterion 3 directly:

| If the browser implements interiors first, and Godot later replaces the browser… | Verdict |
|---|---|
| The room model, `InteriorTemplate` content, `InteriorLocation`, `InteriorPlan`, the entry authority, the hub method | **Kept** — server/contract, unchanged for Godot |
| Server tests, integration tests, the fixture map, the contract validators (`clients/browser/src/contracts/validate.ts`) | **Kept** (or trivially ported — they test the wire) |
| `building-layer.ts` roof toggle, room sprites, Pixi `Container` code | **Thrown away** — but it is 2D tile-sprite drawing, the cheapest possible code to redo |
| The DOM interior panel / HUD wiring | **Thrown away** |
| The camera-zoom-to-building code | **Thrown away** |
| Playwright/fixture visual tests for the browser interior | **Thrown away** |

**The throwaway set is: 2D drawing code, a DOM panel, a camera call, and that client's tests.** That is the whole
cost of being "wrong" about which client goes first. The model, the contract, the payload scoping and the authority
— the parts that are expensive and error-prone to get right once — are indestructible by a client switch.

## 5. The corollary the PO's question misses

The PO's framing ("should this be deferred to after Godot, or is it in the browser's scope?") treats the client
choice as if it gates the *research*. It does not: **the browser and Godot share the model and the contract, and
those are the research.** The client question only decides who writes the ~six renderer/input items in §3, and that
decision is *reversible at the cost of sprite code*. So the client question is a scheduling question, not an
architecture question — which is exactly why the Tech Leader's position ("the research itself should not be
deferred") is correct, and why the sequencing answer belongs in [07](07-recommendation.md), not here.

## 6. One wrinkle worth naming

The `building-selection.ts` entry flow already exists in the browser and **has no equivalent in a Godot client that
does not exist**. If interiors are deferred until Godot lands, the live client keeps shipping an entry flow with
nowhere to go for the whole intervening period — the cost the Tech Leader named. That "nowhere to go" state is a
product defect today (a player walks to a door, presses interact, and gets a `Working` label and no room), and it is
fixed by the same work regardless of client.

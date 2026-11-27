# 04 — Data and simulation model

**Verified against:** `master` @ `dc5f2ce`. This file specifies the model the recommendation
([07](07-recommendation.md)) commits to: it is written as a design a future card can implement, not as prose.

## 1. The room model

The model has three levels, and the middle one is what the codebase does not have today.

```
InteriorTemplate   (authored, one per BuildingKind — 5 total)
      │  instantiated by
      ▼
Interior           (derived per placed building, keyed by NetworkId)
      │  contains
      ▼
Room[]             (rectangles + a role + a binding to the building's own sub-entities)
```

### 1.1 `Room` — the atom

A room is a rectangle in the building's **local interior space** (origin at the building's `Placement.Anchor`,
`Placement.cs:11-13`), a **role**, a **capacity**, and a **binding** to a domain container:

| Field | Meaning | Source in the current model |
|---|---|---|
| `RoomId` | Stable id within the building | new |
| `Role` | What the room *is* (lobby, unit, office-floor, classroom, shop-floor, hall) | new — a small enum, one per `BuildingKind` family |
| `Bounds` | Rectangle in local interior space | new (the footprint envelope is the outer bound, `Placement.Footprint`) |
| `Capacity` | How many can be inside | domain container (`Office.Capacity`, `School.Capacity`, `HousingUnit.Capacity`) |
| `Binding` | The domain entity this room *represents* | `HousingUnit` / `Office` seat group / `School` / none |
| `Doors` | Which other rooms / the outside it connects to | new (the room graph edge) |

The room graph (rooms + door edges) is what lets the client know adjacency and what lets a future pathfinder route
*inside* without a second map.

### 1.2 `Interior` — the instance

An `Interior` is **derived**, not stored, for any building whose kind has a template. It is computed the same way
`WorldMap` is: rebuilt from the world's components on demand and **never serialised as authoritative state**
(`WorldMap.cs:7-13`). It carries the rooms, the template id, and the reference back to the building's `NetworkId`
and `Placement`.

This is the key cost decision: because the interior is *derived* from the template + the building's existing
sub-entities, there is **no new authoritative simulation entity to persist, migrate, or reconcile on restore**. It
is an index, like `HousingRegistry` or `WorldMap`.

### 1.3 `InteriorTemplate` — the layout source

See §2.

## 2. Authored vs procedural

**Recommendation: authored template per `BuildingKind`, parameterised by the building's data. Not per-building
procedural generation.** The reasoning, in order of weight:

1. **The domain already parameterises for us.** A `Property` has `UnitCount`, and each `HousingUnit` has a
   `UnitNumber`, `Owner`, `Capacity` and `Tier` (`HousingUnit.cs:12-24`). An Office has `Capacity`/`SeatsUsed`. A
   School has `Capacity`/`Enrolled`. The rooms are *counted in the data already*; the only thing missing is their
   geometry. A template that says "one unit-room per `HousingUnit`, laid out in a grid inside the envelope"
   converts existing data into rooms with no new simulation.
2. **Five templates is a content bill the project can pay; per-building generation is a *systems* bill it cannot
   validate.** Procedural geometry is hard to make readable, hard to test, and produces a layout nobody designed
   when a room is one tile wide. Authored templates are testable, reviewable and stable.
3. **Repetition is the accepted cost** and is mitigated by parameterisation (a 40-unit block has more rooms than an
   8-unit one) and, later, by per-district variants. The PO should set the expectation that v1 interiors are
   generic ([08](08-open-questions-for-the-po.md)); the prior art's "every shop looks the same" is a feature of the
   template approach, not a bug ([02](02-prior-art.md), Option C).

**Untouched option:** the `Room` list is data, so a future procedural generator can *emit* the same `Room[]` shape
without any consumer changing. The model does not foreclose procedural; it defers it.

## 3. How it fits the existing entities

| `BuildingKind` | Domain component | The rooms represent | Binding |
|---|---|---|---|
| `Residential` | `Property` + `HousingUnit[]` | one room per `HousingUnit` (bedroom/living) + a shared lobby | `HousingUnit` (`UnitNumber`, `Owner`) |
| `Office` | `Office` + `Employee[]` | a floor with desk-space, a meeting room, a lobby | `Office.Capacity`/`SeatsUsed`; `Employee.Office` |
| `School` | `School` | classrooms + a hall | `School.Capacity`/`Enrolled` (`School.cs`) |
| `Store` | (no dedicated component today — `BuildingKind.Store` exists, `BuildingKind.cs`) | a shop floor | none (phase 1: decorative) |
| `Civic` | `SocialVenue` (`Genelife.Domain/Social/VenueService.cs:57-106`) | a hall / venue floor | `SocialVenue` kind (lobby, yard, café, park — `VenueService.cs:66-76`) |

Two observations that make this cheap:

- **`BuildingKind` already partitions the content** (`BuildingKind.cs`), so "one template per kind" is a
  ready-made partition.
- **The `BuildingKind → domain component` mapping already exists** in the building projection: `BuildMapBuilding`
  branches on `Property` / `Office` / `School` to fill the client's `MapBuildingStatus`
  (`SimulationEngine.cs:3283-3321`). The interior binding is the *same switch*, one level deeper.

## 4. Authority for "inside"

**The server owns "inside", full stop.** The client's own contract already says so — *"The client never asserts
range, role or ownership: it shows a proximity hint and reports the server's answer honestly"*
(`building-selection.ts:23-33`) — and the server already enforces the one boundary that exists, the entry check
(`SimulationEngine.cs:1460-1463`).

The model refines, rather than replaces, that authority:

1. **Outside (everyone):** "inside building X" remains **footprint-presence** — a genos' `TilePosition` over the
   building's `Placement.Covers(tile)` (`Placement.cs:37-38`), surfaced today as
   `WorkplaceBuildingNetworkId`/`HousingBuildingNetworkId` (`SimulationEngine.cs:6520-6533`). This does not change.
2. **On entry (the player's genos):** the existing `useBuilding` handling (`SimulationEngine.cs:1416-1517`) gains a
   second effect alongside `CurrentAction`: the server records an **`InteriorLocation`** for the genos —
   `(BuildingNetworkId, RoomId, local position)` — and includes it in the genos snapshot.
3. **Inside (rendering scope):** while the player's genos has an `InteriorLocation`, the client renders the room
   scope for that building and hides the roof; the world keeps ticking.
4. **On exit:** the server clears `InteriorLocation` when the genos' position leaves the footprint/entry, and the
   client falls back to the footprint view. This is a **state transition the server asserts**, never something the
   client infers.

**An `InteriorLocation` is a fourth sibling to the footprint links, not a replacement for them.** The footprint
links answer "which building am I associated with" (a work/home link that survives death, homelessness, job
change); the interior location answers "which room am I standing in right now". They can disagree on purpose: an
unemployed genos has no `WorkplaceBuildingNetworkId` but can still walk into an office and stand in its lobby.

### 4.1 What stays out of the model

- **No per-NPC room simulation.** NPCs stay footprint-based ([01 §3](01-problem-and-current-state.md)); the
  performance ruling forbids the alternative. The `Interior` is a *view* index, not a per-tick system.
- **No client authority over the doorway.** The client may *predict* the walk (it already does,
  `building-selection.ts:127-150`) but the server asserts the crossing, exactly as it asserts range today.
- **No new persisted component required in phase 1.** If `InteriorLocation` is derivable (a genos with
  `CurrentAction = Working` and a `GenerateInterior` request), it can start derived; a persisted field is only
  needed if the room within the building must survive a restart, which phase 1 does not require.

## 5. Open modelling risks

- **Footprint envelopes are not floor plans** (`BuildingFootprints.cs:6-15`): an overlay must subdivide the
  envelope or it shows an open box ([03](03-architecture-options.md), Option B risk). The template's job is exactly
  to subdivide.
- **`Store` has no domain component** (`BuildingKind.Store` exists but nothing projects it in
  `SimulationEngine.cs:3283-3321`); its interior is decorative until a store feature exists.
- **`SocialVenue` vs `Property`/`Office`** both attach to buildings (`VenueService.cs:57-106`); the template
  selection must respect venue-ness first, or a venue will render as an office.

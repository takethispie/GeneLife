# 05 — The contract and the server

**Verified against:** `master` @ `dc5f2ce`. This file answers the card's question *"what the server must own and
send; snapshot/delta cost at scale (thousands of buildings); whether interiors need a new snapshot scope rather than
per-building payloads"*. The answer is: **a new scoped payload, not a per-building one** — the numbers below are why.

## 1. What the server must own

Everything that decides *state*, which is the same rule the client already follows (*"server authority everywhere …
the browser never mutates the world"*, `docs/plans/ROADMAPS.md:90`).

| The server owns | Today | With interiors |
|---|---|---|
| `Placement` (anchor, footprint, entry, kind) | ✔ `WorldMap`, `Placement.cs` | unchanged |
| Building occupancy (`TileCoord → NetworkId`) | ✔ `WorldMap.cs:59-64` | unchanged |
| The entry gate (Chebyshev ≤ 1 to entry) | ✔ `SimulationEngine.cs:1460-1463` | unchanged, reused as the *boundary* |
| Genos → building association (work/home links) | ✔ `GenosStatus`, `SimulationEngine.cs:6520-6533` | unchanged (coarse presence) |
| **Which room the player's genos is in** | ✘ | **new: `InteriorLocation` on the genos snapshot** |
| **The room plan for a building** | ✘ | **new: a scoped `InteriorPlan` payload** |

## 2. What the client can and cannot do

The client is already prevented from *asserting* anything about the world: it walks and reports
(`building-selection.ts:23-33`). Interiors must not be the first place a client gets authority. Concretely, the
client:

- **may** predict the walk (it already does, `building-selection.ts:127-150`) and may render a room plan it has
  been given;
- **may not** decide a genos crossed the doorway, cannot move the genos once inside, and cannot invent a room;
- **must** work from a server-provided plan and a server-asserted interior location, and must fall back to the
  footprint view when it has neither.

## 3. The two payloads

### 3.1 `InteriorLocation` — the tiny, per-genos one

One new nullable field on `GenosStatus` (`Genelife.Contracts/Dtos/GenosStatus.cs`), a sibling to the footprint
links already there (`:25-30`):

```
ulong?  InteriorBuildingNetworkId   // which building (redundant with footprint, but explicit)
int?    InteriorRoomId              // which room
```

It travels in the **existing** world snapshot/delta stream, filtered by viewport like everything else
(`AoiFilter.FilterDeltaByViewport`). Size is negligible (two small integers), and it only appears for genoses that
are actually inside — which is a handful at most, not a per-building cost.

> **Constraint discovered (durable):** `WorldSnapshotDelta.GenosesEqual` is a hand-written structural comparison,
> and `GenosStatusEqualityTests` reflects over the DTO and **fails if any field is omitted** from it — the comment
> on `GenosesEqual` states the failure mode outright: *"A field omitted here is silently never replicated"*
> (`Genelife.Contracts/Snapshots/WorldSnapshotDelta.cs:80-120,138-172`). So any `InteriorLocation` field **must** be
> added to `GenosesEqual` in the same change, or the interior location will never replicate and no compile error
> will say so. This is the single sharpest implementation trap in the whole feature.

### 3.2 `InteriorPlan` — the scoped one

A room plan for **one** building, requested by the client and cached, exactly as the map snapshot is sent once on
connect and cached (`SnapshotBroadcastService.cs:229-282`; `WorldHub.cs:282-293` exposes
`RequestMapSnapshot`). It is **not** broadcast per tick and **not** sent for every building.

## 4. Cost at scale — the argument for a scope, with numbers

The card asks for a *measured* premise. Serialising representative payloads (field shapes taken from
`MapBuildingStatus`, `MapStatus.cs:46-58`, and the `Room` model in [04](04-data-and-sim-model.md)) gives:

| Payload | Bytes | 500 bldgs | 1,000 | 2,000 | 5,000 |
|---|---|---|---|---|---|
| One `MapBuildingStatus` (building list entry) | **182 B** | 89 KB | 178 KB | 356 KB | 889 KB |
| One 6-room `InteriorPlan` | **542 B** | 0.26 MB | 0.52 MB | 1.03 MB | **2.58 MB** |

*(JSON, compact separators; `MapBuildingStatus` = `{networkId, kind, name, district, x, y, w, h, entryX, entryY,
ownerNetworkId}` with `ownerNetworkId: null`; `InteriorPlan` = `{buildingNetworkId, template, rooms:[6 ×
{roomId, role, x, y, w, h, capacity, binding}]}`. The building footprint sizes that make these counts plausible are
in `BuildingFootprints.cs:6-31`; the map is 32000×32000 = 1,024,000,000 tiles since the world-scale
migration — 256×256 = 65,536 tiles before it.)*

**Read this honestly:**

- The building *list* is 182 B/building and **already ships today** as a once-per-connect `MapSnapshot`, with
  `MapDelta` afterwards (`MapSnapshot.cs`, `MapDelta.cs`, `SnapshotBroadcastService.cs:260-330`). Even at 5,000
  buildings that is < 1 MB, sent once — which is why the current design ships it and is fine.
- An **`InteriorPlan` for every building is ~3× the building list** (542 B vs 182 B), because a 6-room plan is
  ~3× a building's metadata. At 5,000 buildings, "send every interior" is **~2.6 MB per full send**, and — crucially
  — it would be **re-sent to every client at every connect** (and on every world reset, `SnapshotBroadcastService.cs:229-282`),
  because the map snapshot is not delta'd field-by-field; the delta only handles added/updated/removed *buildings*.

**Therefore:** shipping interiors as a per-building field on the map payload is a **structurally wrong choice** —
it multiplies the once-per-connect cost of the whole town by an interior that a client will look at for one
building at a time, and it grows the reconnect payload for every player. The correct shape is a **scope**: the plan
for the building the client is in, requested on entry, cached, and dropped on exit.

## 5. The new scope, concretely

Add a **third replication scope** alongside world (dynamic, 40 ms) and map (static, once + deltas):

| Scope | Trigger | Payload | Lifetime |
|---|---|---|---|
| World | every broadcast tick (40 ms) | genoses + companies (delta) | streaming |
| Map | connect / layout change | RLE tiles + building list (delta) | once per connect |
| **Interior** | **client enters a building** | **one `InteriorPlan` + room occupancy** | **until the client leaves** |

- **Request/response, not broadcast.** Reuse the `RequestMapSnapshot` pattern (`WorldHub.cs:282-293`): a client
  that has an `InteriorLocation` requests the plan for that building. Server-authoritative: the server refuses to
  send a plan for a building the genos is not in.
- **Room occupancy** (who else is in the room) rides the world delta as `InteriorLocation` on the relevant genoses,
  already viewport-filtered — so a busy interior is not a new bus, it is the existing one narrowed.
- **Cache and invalidate.** The plan is invalidated if the building is relocated (`RelocateBuildingCommand`,
  `SimulationEngine.cs:2058-2089`) or removed (`BuildingRemoved`, `SimulationEngine.cs:948-953`) — the same events
  the map delta already watches (`SnapshotBroadcastService.cs:260-330`).

## 6. What the server must *not* send

- **A plan per building in the map payload** — the 2.6 MB/connect failure above.
- **A per-tick interior broadcast.** The world tick is 40 ms (`SnapshotBroadcastService.cs:28`); the interior state
  is coarse (which room) and changes only on a doorway crossing, which the world delta already carries.
- **Per-NPC room state.** Forbidden by the performance ruling and unnecessary — see [04 §4.1](04-data-and-sim-model.md).

## 7. Server-side work list (for the implementing card, not done here)

1. `InteriorTemplate` (5, authored) + `Interior`/`Room` derived index — [04](04-data-and-sim-model.md).
2. `InteriorLocation` on `GenosStatus` **and** on `GenosesEqual` (the trap in §3.1).
3. Set/clear `InteriorLocation` in the existing `ApplyUseBuildingIntent` path (`SimulationEngine.cs:1416-1517`)
   and on leaving the footprint.
4. `InteriorPlan` DTO + a `RequestInteriorPlan(buildingNetworkId)` hub method, gated on the genos being inside.
5. Invalidate the plan on relocate/remove.

None of this is in this card's scope (documentation only, acceptance criterion 5); it is the shape the
recommendation in [07](07-recommendation.md) hands to a future implementer.

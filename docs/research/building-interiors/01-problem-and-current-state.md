# 01 — The problem and the current state

**Verified against:** `master` @ `dc5f2ce`.

## 1. What "interior" means here

An **interior** is a *space a genos can occupy that is not a tile of the world map*. It is what the player expects
to see when the character they control walks to a door and goes in: a floor plan with rooms, walls, maybe furniture
or other occupants — a place with an inside, rather than a sprite that disappears behind a roof.

That framing already contains the three sub-problems this dossier has to resolve, and they are independent:

1. **The space model** — what an interior *is*, geometrically and in the data (a room graph? a sub-grid? a separate
   map? authored or generated?).
2. **The transition and the UX** — how a genos gets from "at the entrance" to "inside", and what the player sees
   while crossing that boundary.
3. **The authority and the contract** — who decides a genos is "inside", and what the server must send the client
   so it can draw the interior.

A fourth sub-problem — **which client renders it** — is the one the PO actually asked, and it is genuinely separate
from the first three. §2 shows why: the first three are already half-answered in the repository, and every answer
lives on the server side of the wire.

## 2. What exists today, with evidence

### 2.1 The world is a single deterministic tile map; a building is a footprint on it

- A world is a **256×256 tile map centred on the origin**, integer tile coordinates, 1.0 world unit per tile
  (`Genelife.Domain/Map/MapOptions.cs:14-15,20-23,34-35`).
- A building is **one `Placement`**: an anchor tile, a rectangular `Footprint`, an `Entry` tile, and a
  `BuildingKind` (`Genelife.Domain/Map/Placement.cs:7-29`). `Placement.Covers(tile)` is the entire notion of "is
  this tile part of the building" (`Placement.cs:37-38`).
- `BuildingKind` is a closed set — `Residential, Office, School, Store, Civic`
  (`Genelife.Domain/Map/BuildingKind.cs`).
- The **derived static index** over placements is `WorldMap`; it owns occupancy (`TileCoord → NetworkId`), lot
  reservations and per-tile flags, and exposes `BuildingAt(tile)` and `IsWalkable(tile)`
  (`Genelife.Domain/Map/WorldMap.cs:14-22,89-95,59-64`). It is explicitly "never serialized as authoritative state"
  (`WorldMap.cs:7-13`) — it can be rebuilt from the entity components.
- A footprint is an **envelope sized to content, not a floor plan**: a residential building's footprint is
  `4 + units/10` wide by `3 + units/20` high, capped at 8×5
  (`Genelife.Domain/Map/BuildingFootprints.cs:6-15`). Offices grow with capacity and schools with enrolment
  (`BuildingFootprints.cs:17-31`). **Nothing inside that rectangle exists in the model today.**

### 2.2 "Inside" already has a meaning in the sim — as footprint presence

The just-shipped occupancy work defines being inside a building *only* as a genos' tile falling within the
building's footprint, and surfaces it to clients as nullable link fields on the genos snapshot:

- `GenosStatus` carries `HousingBuildingNetworkId`, `WorkplaceBuildingNetworkId`, `HousingUnitNumber` and
  `JobPosition` (`Genelife.Contracts/Dtos/GenosStatus.cs:25-30`, file has CRLF line endings).
- They are projected from the domain in one place — the genos snapshot builder — where an employee's `Office` and a
  resident's `HousingUnit.Property` are resolved to a placed building's network id via `PlacedBuildingNetworkId`
  (`Genelife.Application/Services/SimulationEngine.cs:6520-6533`, helper at `:6549`). A missing, dead, unplaced or
  unregistered building yields `null` — the client's "unknown" — rather than a dangling id.

So the *link* from a genos to the building it is in already exists. What does not exist is any notion of a **space
inside** that building.

### 2.3 Entering a building is already half-built — and the half that is built stops at the door

- The client's `BuildingSelectionController` walks the genos to a building's `entryX/entryY` when out of range and
  then submits a `useBuilding` intent, with a timeout, cancellation and honest reporting of the server's refusal
  (`clients/browser/src/input/building-selection.ts:23-34,93-112,127-180`). The class doc is explicit: *"The client
  never asserts range, role or ownership"* (`building-selection.ts:23-33`).
- The server refuses a `useBuilding` intent unless the genos is within **Chebyshev distance 1** of the entry tile
  (`Genelife.Application/Services/SimulationEngine.cs:1460-1463`, `"Walk to the entrance first"`), then switches the
  genos' `CurrentAction` to `Sleeping` (a `Property`), `Working` (an `Office`, only if the genos is its employee) or
  `Studying` (a `School`), with a `role_not_allowed` refusal otherwise
  (`SimulationEngine.cs:1493-1515`). For a `SocialVenue` building it instead hands off to `VenueService.TryEnter`
  and clears the move target (`SimulationEngine.cs:1466-1492`).
- The emit path is a `UseBuildingIntent` carrying `GenosNetworkId` and `BuildingNetworkId`
  (`Genelife.Contracts/Intents/Intent.cs:27-31`), submitted through `IntentClient.useBuilding`
  (`clients/browser/src/net/intent-client.ts:126-131`).

**The critical observation:** today "entering" is an *action label on a genos that never leaves the map*. The genos
stops on the entrance tile and gets `CurrentAction = Working/Sleeping/Studying`; the building is drawn as a solid
footprint with a roof (`clients/browser/src/render/map/building-layer.ts`), and the player sees a status change, not
a room. The entry flow has nowhere to *go*.

### 2.4 How the map and the building list reach the client

- The static map is sent once per connect as a `MapSnapshot`: run-length-encoded tile runs plus a `List<MapBuildingStatus>`
  (`Genelife.Contracts/Dtos/MapSnapshot.cs:8-29`).
- `MapBuildingStatus` is the per-building client shape: `NetworkId, Kind, Name, District, X, Y, Width, Height,
  EntryX, EntryY, OwnerNetworkId?` (`Genelife.Contracts/Dtos/MapStatus.cs:46-58`). It is built by
  `SimulationEngine.BuildMapBuilding` from the `Placement` plus the `Property`/`Office`/`School` component
  (`SimulationEngine.cs:3283-3321`).
- Changes ship as a `MapDelta` (`added` / `updated` / `removed`, `MapDelta.cs:3-12`), computed by diffing the
  current building list against the last broadcast one
  (`Genelife.API/Services/SnapshotBroadcastService.cs:260-330`). The map snapshot is **not** re-sent on every tick:
  it is sent on connect, on a world reset and when the layout changes; otherwise only deltas go out
  (`SnapshotBroadcastService.cs:229-282`).
- The dynamic stream is the **world snapshot** (genoses + companies), broadcast every **40 ms**
  (`SnapshotBroadcastService.cs:28,44-52`), with per-connection viewport filtering of genoses via `AoiFilter`
  (`Genelife.Application/Snapshots/AoiFilter.cs`; registry at `Genelife.Application/Snapshots/ViewportRegistry.cs`).
- The client mirrors these types by hand in `clients/browser/src/contracts/hub.ts:19-56` (with the note that the
  JSON fixtures under `src/test/fixtures` fail loudly if the shape drifts).

### 2.5 The domain shapes interiors must fit

The entities a room model has to sit beside:

| Entity | File | Carries |
|---|---|---|
| `Property` (a residential building) | `Genelife.Domain/Components/Housing/Property.cs:10-19` | `Name, District, Owner, UnitCount, Condition, NeighbourhoodScore, MonthlyUpkeep, IsShelter` |
| `HousingUnit` (a tenancy inside a property) | `Genelife.Domain/Components/Housing/HousingUnit.cs:12-24` | `Property` (back-link), `UnitNumber`, `Tier`, `Capacity`, `BaseRent`, `Condition`, `IsVacant`, `Owner`, `PurchasePrice`, `OccupantCount` |
| `Office` (a company workplace) | `Genelife.Domain/Components/Company/Office.cs` | `OfficeName, Company, Capacity, SeatsUsed, OperatingCostPerCycle, IsClosed` |
| `Employee` (a genos ↔ workplace link) | `Genelife.Domain/Components/Employment/Employee.cs:11-18` | `Company, Office, Position, SalaryPerCycle, Seniority, HiredAtTick` |
| `School` | `Genelife.Domain/Components/Life/School.cs` | `Name, District, Capacity, Quality, Emphasis, MonthlyCost, Owner, Enrolled` |

Note the shape of this: a **`Property` already has `UnitCount` units, and each `HousingUnit` carries a
`UnitNumber`** — a residential building is already a *container of named sub-spaces* in the housing model, even
though those sub-spaces have no coordinates. An office has `Capacity`/`SeatsUsed`. So the domain already thinks in
terms of "the interior of a building is divided into a counted number of named units" — it just never gave those
units a place.

## 3. Does an interior replace, refine, or coexist with footprint-presence?

This is the question the card asks to answer explicitly, and the answer is **refine, not replace** — and it splits by
*who* is being simulated:

- **The macro simulation (every NPC) keeps footprint-presence.** It is O(1) per query, already wired into the
  occupancy snapshot (`SimulationEngine.cs:6520-6533`) and into presence/venue logic, and it is the only thing that
  scales to thousands of genoses: the performance ruling of 2026-11-27 explicitly forbids spending the tick budget
  on anything before the Godot client is at parity, and a per-NPC room simulation would spend it.
- **Interiors refine presence for the one genos the player is driving (and, at most, for the occupants of the room
  the player is in).** When the player is inside a building, "which building am I in" is already answered by the
  footprint; the interior adds "which room, and what is in it" *for that scope only*.

Any option that simulates every NPC inside a room plan would be a performance-roadmap feature by the back door, and
is out of scope by the standing ruling. This constraint is what eliminates the most ambitious architecture option in
[03](03-architecture-options.md) and shapes the recommendation in [07](07-recommendation.md).

## 4. What is missing, in one list

1. A **space model**: what a room is, how rooms relate to a building's footprint and to its housing units / office
   seats.
2. An **interior payload / scope**: how a room plan or room *state* reaches a client without shipping a plan for
   every building.
3. A **transition contract**: what the server tells the client when a genos crosses the doorway, and what the client
   is allowed to show (instanced cutaway vs separate scene).
4. A **client renderer** per client (browser today; Godot when it exists).

Items 1–3 are client-agnostic. Item 4 is the only client-specific one, and it is the throwaway half
([06](06-client-implications.md)).

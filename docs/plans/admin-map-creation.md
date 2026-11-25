# Admin "Create Map" (World Reset) Plan

Status: Implemented
Scope: `Genelife.Web` admin UI, `Genelife.API`, `Genelife.Application`, `Genelife.Contracts`, replication/read models
Goal: Let an admin create a new world map from the admin interface. If a map already exists, require an explicit confirmation and then replace the map, buildings, NPCs, and player characters.

---

## 1. Required behavior

1. The `/map` admin page gets a **Create map** action.
2. When no map exists, the action creates the map and starts the world without a confirmation step.
3. When a map already exists, clicking the action opens a **confirmation dialog**. Nothing is destroyed until the admin confirms.
4. On confirmation the server:
   - tears down the current world,
   - generates a new map,
   - regenerates buildings, NPCs, companies, housing, and schools,
   - recreates the player character for each active/known player,
   - persists the new world,
   - refreshes replication so clients see the new map and world state.
5. The API must also enforce confirmation; the dialog is not the only guard.
6. Cancelling the dialog must leave the current world untouched.

---

## 2. Current behavior (evidence)

### 2.1 Where the map exists today

- The map is generated only during engine start:
  - `SimulationEngine.Initialize` calls `MapContext.Generate(...)` at `Genelife.Application/Services/SimulationEngine.cs:285` and `:308`.
  - `MapContext.Generate` is at `Genelife.Domain/Map/BuildingPlacementService.cs:154-165`.
  - `MapGenerator.Generate` builds terrain, roads, lots, zones, and spawn points at `Genelife.Domain/Map/MapGenerator.cs:21-44`.
- Start/stop are synchronous lifecycle operations:
  - `POST /api/simulation/start|stop` at `Genelife.API/Endpoints/Wolverine/SimulationEndpoints.cs:17-37`.
  - `SimulationManager.StartSimulation/StopSimulation` at `Genelife.Application/Services/SimulationManager.cs:84-102`.
- There is no admin "create map" endpoint today. `MapEndpoints` only reads the map and places/relocates buildings: `Genelife.API/Endpoints/Wolverine/MapEndpoints.cs:13-114`.
- Because `Shutdown` nulls `mapContext` (`SimulationEngine.cs:556`), a map exists in memory only while the simulation runs. `BuildMapStatus` returns an empty `MapStatus` when `mapContext == null` (`SimulationEngine.cs:2953-2958`).

### 2.2 Why this cannot be an AdminCommand

- Normal admin mutations are queued and applied at tick start: `SimulationEngine.cs:1843-1897`.
- That path runs inside the tick's write lock, and it rejects commands when the engine is not running (`SimulationEngine.cs:1857-1865`).
- Map creation needs `Shutdown` + `Initialize` (rebuild world, economy, systems, network-id scope). It must run as a lifecycle operation outside `Tick`, not as a normal admin command.

### 2.3 Where buildings, NPCs, and players come from

- Map generation creates terrain/lots/spawn points only; it never creates buildings (`MapGenerator.cs:6-9`).
- Buildings and NPCs are produced by `PopulationGenerator.Generate`:
  - `Genelife.Application/Services/PopulationGenerator.cs:77-210`.
  - Schools: `EnsureSchools` `:422-445`.
  - Companies/offices: `EnsureCompanies` `:486-561`.
  - Housing/properties: `EnsureHousing` `:563-596`.
- Player sims are created on hub connection and via `EnsurePlayerSim`/`EnsurePlayerSpawn`:
  - `Genelife.API/Hubs/WorldHub.cs:88-90`.
  - `SimulationEngine.cs:1605-1650` and `:1813-1834`.
- The repository already has a canonical reset sequence in `tools/preview/reset.sh`: stop, start (new map), generate population, save snapshot. This feature productizes that flow.

### 2.4 UI patterns already available

- The admin UI is Blazor WebAssembly: `Genelife.Web`.
- `Map.razor` already uses two-step inline confirmation for relocation: `Genelife.Web/Pages/Map.razor:143-200`.
- `Family.razor` uses a checkbox gate for destructive actions: `Genelife.Web/Pages/Family.razor:732`.
- There is **no reusable modal dialog component** in `Genelife.Web` today.
- The admin HTTP facade is `Genelife.Web/Services/GameDataService.cs`; admin requests use `EnsureAdminTokenAsync` (`:800-809`) and local-admin auth (`:811-826`).
- The shared `HttpClient` has a 10-second timeout (`GameDataService.cs:29`), which matters for a reset operation.

### 2.5 Replication and read-model constraints

These must be handled or a reset will leave live clients stale:

- Clients reject a full snapshot whose tick is older than the last applied tick (`clients/browser/src/simulation/store.ts:161-164`).
- Clients reject map deltas when `mapId` changes or `fromVersion` does not match the cached version (`clients/browser/src/simulation/map/apply-map-delta.ts:15-30`).
- `SnapshotBroadcastService` only emits map deltas and skips work when `engine.MapVersion` is unchanged (`Genelife.API/Services/SnapshotBroadcastService.cs:212-252`).
- `MapProjection.Project(MapGenerated)` only creates a `MapDocument` when none exists; otherwise it updates `Version`/`UpdatedAt` only (`Genelife.Infrastructure/Projections/MapProjection.cs:14-41`), leaving stale seed/dimensions after a resize.
- `NetworkIdRegistry.Clear()` clears mappings but **does not reset the id counter** (`Genelife.Application/Services/NetworkIdRegistry.cs:91-95`), so new worlds in the same process get fresh network ids and do not collide with old building documents.
- The browser client only learns its new player sim id from `Welcome` (`clients/browser/src/app/bootstrap.ts:553-576`).

---

## 3. Interpretation and design decisions

### 3.1 "Recreate the buildings, the NPCs, and the player character"

Recommended interpretation: a **fresh world populated from scratch**.

- Terrain/lots/spawn points are regenerated.
- Buildings are recreated by `PopulationGenerator` (schools, company offices, housing properties).
- NPCs are recreated by `PopulationGenerator` with the requested population seed/size.
- Player characters are recreated for every currently connected player and every player profile known before the reset.
- Manually created or previously simulated entities are discarded. This is why the confirmation is destructive.

This matches the existing `reset.sh` flow and the domain's separation between map generation and population generation.

### 3.2 Lifecycle endpoint, not an AdminCommand

Add a synchronous admin endpoint plus a `SimulationManager` orchestration method. It reuses `Shutdown`/`Initialize` like start/stop, but adds population generation and player respawn. Do **not** add a new `AdminCommand`, which would be rejected while stopped and would run inside the tick lock.

### 3.3 Keep the world replication-compatible

To avoid a new client epoch/versioning system:

- Preserve tick monotonicity across the reset: add an optional `startTick` to `Initialize` and start the new world at `previousTick + 1`.
- Keep `MapId = "world"` for storage continuity, but set `MapOptions.Version` to `previousMapVersion + 1` so map versions are monotonic and a full map snapshot can be distinguished from a delta.
- After reset, broadcast a **full** map snapshot (layout changed) and let the normal snapshot broadcast deliver a full world snapshot because `SnapshotHistory` was cleared.
- Add an explicit reset notification so connected browser clients can refresh their `Welcome`/own-sim id and request map/world snapshots.

---

## 4. Admin UX

### 4.1 Placement

On `/map`, add a "World generation" card/button cluster. Two reasonable layouts:

- **Header button**: `Create map` next to `Refresh` (`Genelife.Web/Pages/Map.razor:15-30`) for the happy path.
- **Settings card**: seed, width, height, population, population seed, optional adults/children. Keep it collapsed under an "Advanced" toggle so the common action stays simple.

Suggested defaults:

- `seed` = current map seed once `MapStatus.Seed` is added; otherwise `1`.
- `width`/`height` = current `map.Width`/`map.Height`; default `256`.
- `population` = current living sim count (clamp to a safe minimum such as `500` when zero or unknown).
- `populationSeed` = same as map seed.
- `adults`/`children` = omitted (generator defaults).

### 4.2 Flow

1. Page loads `/api/map` and `/api/data` as it does today.
2. Admin clicks **Create map**.
3. If `map.HasMap` is true (or `world.IsRunning`), show the confirmation dialog with:
   - Title: "Replace the existing map?"
   - Body: warns that the current map, all buildings, all NPCs, and player characters will be permanently replaced.
   - Summary: seed, dimensions, population size.
   - Buttons: `Cancel` and destructive `Replace map`.
4. If no map: skip the dialog and submit immediately.
5. While submitting: disable the action, show a spinner/progress message, suspend or tolerate the 2-second refresh loop.
6. On success: refresh `/api/map` and `/api/data`; show a success alert with map size, seed, building count, population, and recreated player count.
7. On `409` (server still requires confirmation): reopen/show the dialog.
8. On `4xx/5xx`: show the error message and keep the current UI state.

### 4.3 Confirmation dialog component

Add `Genelife.Web/Components/ConfirmDialog.razor` (plus `.css`):

- Parameters: `Title`, `ChildContent`/`Message`, `ConfirmLabel`, `CancelLabel`, `IsBusy`, `IsDestructive`, `OnConfirm`, `OnCancel`.
- Render a Bootstrap-styled overlay (`position: fixed; inset: 0; background: rgba(...)`) containing a card. No JS interop is required; the component is toggled by Blazor state.
- Accessibility: `role="dialog"`, `aria-modal="true"`, `aria-labelledby`, focus the cancel/confirm button on open. Support Escape to cancel.
- Use it for map replacement. Refactoring the relocation inline confirmation is optional and out of scope.

---

## 5. Backend design

### 5.1 Contracts

Add to `Genelife.Contracts/Dtos/MapStatus.cs`:

- `bool HasMap` — true when an in-memory map exists.
- `int Seed` — map seed (currently absent from `MapStatus`; `MapSnapshot` has it at `Genelife.Contracts/Dtos/MapSnapshot.cs:7-21`).
- `int LayoutVersion` — `MapOptions.Version` (distinct from the placement-incremented `Version`).

Add a shared request/response DTO, e.g. `Genelife.Contracts/Dtos/MapCreation.cs`:

```csharp
public sealed record CreateMapRequest(
    int Seed,
    int Width,
    int Height,
    int Population,
    int PopulationSeed,
    int? Adults,
    int? Children,
    bool Confirm);

public sealed record CreateMapResult(
    string MapId,
    int Version,
    int LayoutVersion,
    int Seed,
    int Width,
    int Height,
    int Buildings,
    int Population,
    int Players,
    long ElapsedMs);
```

Putting these in Contracts lets both API and Web use the same types (existing request records such as `PlaceBuildingRequest` are duplicated in the API endpoint and `GameDataService` because they are not shared).

### 5.2 API endpoint

Add to `Genelife.API/Endpoints/Wolverine/MapEndpoints.cs`:

```csharp
[WolverinePost("/api/map/create")]
[Authorize(Policy = AuthPolicies.Admin)]
public static IResult CreateMap(
    CreateMapRequest request,
    SimulationManager simulationManager,
    IConnectionRegistry connections,
    HttpContext http)
```

Behavior:

1. Validate:
   - `Width`/`Height` within safe bounds (suggest `64..512`; reject larger to bound generation time).
   - `Population` within safe bounds (suggest `0..5000`).
   - `PopulationSeed` any `int`; `Adults`/`Children` non-negative when provided and not greater than `Population`.
2. Determine `hasMap = simulationManager.HasMap`.
3. If `hasMap && !request.Confirm`, return `409 Conflict` with a body such as `{ message = "A map already exists; confirmation is required.", requiresConfirmation = true }`. Do not enqueue or mutate anything.
4. Capture connected player ids: `connections.GetPlayerIds()`.
5. Call `simulationManager.CreateMap(request, playerIds, issuedBy, cancellationToken)`.
6. Return `200 OK` with `CreateMapResult`.
7. On validation/generation failure return `400`/`500` with a clear message; log the failure with `issuedBy`.

No rate limiter is needed beyond the Admin policy. The endpoint is synchronous and should be documented as a rare, destructive admin operation.

### 5.3 `SimulationManager.CreateMap`

Add a serialized lifecycle operation:

```csharp
private readonly SemaphoreSlim lifecycleGate = new(1, 1);

public bool HasMap => engine.MapContext is not null;

public CreateMapResult CreateMap(
    CreateMapRequest request,
    IReadOnlyCollection<string> connectedPlayerIds,
    string issuedBy,
    CancellationToken cancellationToken = default);
```

Also wrap `StartSimulation`, `StopSimulation`, `RestoreSnapshot`, and `CreateMap` in `lifecycleGate` so concurrent start/stop/create requests cannot race. `StartAsync` at app startup can use the same gate or remain as-is because it runs before requests.

Implementation sequence (all inside the gate):

1. **Capture old state before teardown**
   - `previousTick = engine.CurrentTick`.
   - `previousMap = engine.BuildMapStatus()`; use its `Version`/`LayoutVersion`.
   - Old placement ids: `engine.MapContext?.Map.Placements.Keys.ToList()`.
   - Player ids: union of `connectedPlayerIds` and `engine.Players.All().Select(p => p.PlayerId)`.
   - Current population count for defaults if needed.
2. **Validate/generate safely**
   - Build `MapOptions`:
     - `MapId = "world"` (continuity).
     - `Version = Math.Max(1, previousMap.Version + 1)` (monotonic map version).
     - `Seed = request.Seed`, `Width`, `Height`, `TileSize = 1f`.
   - Keep `MapGenerationOptions` defaults for v1 (road spacing, water fraction, zone shares).
3. **Tear down**
   - If running: `StopPeriodicSave(); engine.Shutdown(); networkIds.Clear();` (or call `StopSimulation`).
   - Clear transient queues that belong to the old world: pending admin commands, queued player commands, intent reactions, lifecycle notices, command results, seen command ids. Add a public `SimulationEngine.ClearTransientState()` and call it here. This prevents old pending commands from applying to the new world.
4. **Create the new world**
   - Set `engine.MapOptions = newOptions;`.
   - Set `engine.RandomSeed = request.PopulationSeed` (or map seed) before `Initialize`, so economy randomness is deterministic.
   - Add `long startTick = 0` to `SimulationEngine.Initialize(...)` and set `tick = startTick` instead of `0`. Call `engine.Initialize(startTick: previousTick + 1)`.
   - `StartPeriodicSave()`.
5. **Recreate buildings/NPCs**
   - Add a public `SimulationEngine.GeneratePopulation(int totalSims, int seed, int? adults, int? children)` that takes the engine write lock and reuses the existing `PopulationGenerator` code from the `GeneratePopulationCommand` case (`SimulationEngine.cs:2144-2162`). Refactor the command case to call it so there is one path.
   - Call it only when `Population > 0`.
   - On placement failure (`PopulationGenerator` throws when a school/office/property cannot be placed), catch, `engine.Shutdown()`, and return a clear error. The old world is already gone; the admin must retry with a different seed or larger map. Surface this in the dialog copy.
6. **Recreate player characters**
   - For each distinct player id: `engine.EnsurePlayerSim(playerId)` then `engine.EnsurePlayerSpawn(playerId)`.
   - `PlayerRegistry` is cleared by `Initialize`, so these create fresh sims. If no players are connected/known, zero players are created.
   - Record the new sim network ids for the reset notification.
7. **Persist**
   - `SaveSnapshot()` so the reset survives an API restart (matches `reset.sh`).
   - `await SaveWorldDocumentsAsync()` so Marten world documents are updated (optional if the event writer catches up; do it for determinism).
8. **Clean stale map read models**
   - Emit `BuildingRemoved(oldMapId, oldVersion, id, tick)` for each old placement id (or add a dedicated `MapCleared` event plus projection cleanup). This prevents stale `BuildingDocument` rows from a previous world.
   - Because network ids keep increasing in-process, old and new building ids do not collide.
9. **Notify replication**
   - Call a reset notification (`IWorldResetNotifier`/`SnapshotBroadcastService.NotifyWorldReset(...)`).
   - The notifier broadcasts a `WorldReset` payload and/or a full `SendMapSnapshot`; the next broadcast sends a full world snapshot because `SnapshotHistory` was cleared.
10. **Build and return `CreateMapResult`**
    - Use `engine.BuildMapStatus()` after population generation for building/unplaced counts, `engine.GetLatestSnapshot()?.Sims.Count`/life index for population, and the captured new player ids.

### 5.4 Engine changes

In `Genelife.Application/Services/SimulationEngine.cs`:

1. `Initialize(..., long startTick = 0)` — set `tick = startTick` at the end instead of `tick = 0`.
2. `public PopulationGenerationResult GeneratePopulation(...)` — shared implementation for the admin command case and reset.
3. `public void ClearTransientState()` — clear `adminCommands`, `queuedCommands`, `commandResults`, `seenAdminCommands`, `intentRejections`, `playerLifecycleNotices`, pending player command counters, and any stale outbound events. Call it from the reset orchestration. Do not clear persistent player profiles here; `Initialize` already resets the registry.
4. `BuildMapStatus` — populate `HasMap = true`, `Seed = options.Seed`, `LayoutVersion = options.Version`.
5. Optional: expose `MapOptions`/`MapGenerationOptions` validation helpers (width/height bounds) so the endpoint and manager share them.

### 5.5 Replication and read models

**`Genelife.Infrastructure/Projections/MapProjection.cs`**

- When `MapGenerated` arrives for an existing `MapId`, overwrite `Version`, `Seed`, `Width`, `Height`, `TileSize`, `MinX/MinY/MaxX/MaxY`, and `UpdatedAt`. Today only version/time change.
- Keep `BuildingPlaced` upsert-by-network-id and `BuildingRemoved` delete behavior.

**`Genelife.API/Services/SnapshotBroadcastService.cs`**

- Track `lastMapSeed`, `lastMapWidth`, `lastMapHeight`, and `lastLayoutVersion` in addition to `lastMapVersion`.
- When any of those change (a new layout), send a full `SendMapSnapshot` to all clients and reset `lastMapBuildings`, instead of emitting only a `MapDelta`.
- Defensively reset `lastBroadcastedTick` if the incoming snapshot tick is lower than the last broadcast (in case tick continuity ever changes).
- Expose a public `NotifyWorldReset(...)` or implement an `IWorldResetNotifier` abstraction so `SimulationManager` can trigger the broadcast without taking a hard dependency on SignalR internals.

**Connected game clients (required for a live world)**

- Add a `WorldReset` hub event and a `RequestWelcome` hub method (extract the `Welcome` construction from `WorldHub.OnConnectedAsync` into a reusable helper).
- Server: after reset, broadcast `WorldReset` with `mapId`, `mapVersion`, `layoutVersion`, `seed`, `tick`, and the new player sim ids where possible. For per-player sim ids, either:
  - send a personalized `Welcome` to each connection, or
  - have clients call `RequestWelcome` when they receive `WorldReset`.
- Browser client: on `WorldReset`, clear/replace local map and world state, call `RequestWelcome`, `RequestMapSnapshot`, and `RequestSnapshot(null)`, and re-run the loading/selection reconciliation.
- If the browser client is not updated in the same change, document that connected clients must reconnect/reload after an admin map reset. Do not silently leave them on stale state.

---

## 6. Persistence and events

- `Initialize` emits `MapGenerated` (`SimulationEngine.cs:985-998`), which creates/updates `MapDocument`.
- `CollectMapEvents` emits `BuildingPlaced` on the next tick after population generation (`SimulationEngine.cs:939-1013`), so the event store and Marten projections catch up.
- Explicit `BuildingRemoved` events clean old `BuildingDocument` rows.
- `SaveSnapshot` writes the new `PersistedWorldState`, so a later API restart restores the new world (not the old snapshot).
- Existing snapshot history for the old world is not required; `SnapshotHistory.Clear()` runs in `Initialize`.

---

## 7. Validation, security, and limits

- Endpoint policy: `AuthPolicies.Admin`.
- Use `http.User.Identity?.Name ?? "admin"` as `issuedBy`; log reset start/success/failure with counts.
- Validate dimensions and population before touching the world.
- Suggested bounds: width/height `64..512`, population `0..5000`. Reject outside these with `400`.
- Confirmation is mandatory on the server when a map exists; never trust the UI flag.
- Bound reset duration by capping population and dimensions. If a future population limit is large, switch the endpoint to `202 Accepted` plus a status endpoint; v1 can stay synchronous.
- The Web `HttpClient` timeout is 10 seconds (`GameDataService.cs:29`). Either:
  - add a dedicated long-running admin client/`HttpClientFactory` registration with a 2-minute timeout, or
  - raise the admin client timeout for this screen.
  Document the chosen approach. Prefer a dedicated client so normal admin requests keep the short timeout.

---

## 8. Edge cases and failure handling

| Case | Handling |
| --- | --- |
| Simulation stopped / no map | No dialog; create, start, populate, save. `HasMap` is false. |
| Map exists | Dialog required; server `409` without `confirm=true`. |
| No connected/known players | `Players = 0`; NPC population still generated if requested. |
| `Population = 0` | Map only; no buildings/NPCs; player sims still recreated. |
| Map too small for generated buildings | Population generator throws on placement; shut down the new world and return a clear error. Recommend a different seed/larger map. |
| Pending admin/player commands from old world | Cleared by `ClearTransientState` before rebuilding. |
| Periodic save running during reset | Stop the save timer before `Shutdown`; restart after `Initialize`; save once at the end. |
| Concurrent start/stop/create requests | Serialize through `lifecycleGate`. |
| MapPage 2-second refresh during reset | Suspend/guard refresh while the create request is in flight; refresh after completion. |
| Old Marten building docs | Emit `BuildingRemoved` for old placements or add `MapCleared` projection cleanup. |
| Stale `MapDocument` seed/dimensions | Update projection to overwrite metadata on regeneration. |
| Live clients on old tick/map | Send full map snapshot and full world snapshot; add `WorldReset`/`RequestWelcome` handling. |
| Reset fails halfway | `Shutdown()` the new world; return error. The old world is already gone; the admin retries with new inputs. |
| Snapshot save fails | Return a warning/error; the world is running in memory but not durable. Log it. |

---

## 9. Testing plan

### 9.1 Unit / engine tests (fast tier)

Add `Genelife.Domain.Tests/Services/MapCreationTests.cs` (Domain.Tests references the API/Application assemblies):

1. `CreateMap` with a new seed produces a map with that `MapOptions.Seed`, a larger `Version`, and `HasMap == true`.
2. Population generation creates buildings and NPCs; `BuildMapStatus().BuildingList` is non-empty and the sim count matches the request (subject to generator result).
3. Player ids passed in produce new `PlayerControlled` sims and spawn positions after reset.
4. Old placement network ids are no longer in `MapContext.Map.Placements`; explicit removals are emitted if implemented.
5. Tick monotonicity: new world tick > previous tick.
6. Pending admin commands are not applied after reset.
7. Reset when stopped works (no `isRunning` precondition failure).
8. Population zero leaves a valid empty map and running engine.

Extend existing map tests:
- `Genelife.Domain.Tests/Map/MapFoundationTests.cs` for seed/dimensions/signature behavior.
- `Genelife.Domain.Tests/Docs/DocsConsistencyTests.cs` only if a new route is added to the enforced route list.

### 9.2 API integration tests

Add `Genelife.Api.IntegrationTests/Endpoints/Wolverine/MapCreationEndpointTests.cs`:

1. Anonymous/player request → `401`/`403`; admin request → success.
2. Existing map + `confirm=false` → `409` with `requiresConfirmation`.
3. Existing map + `confirm=true` → `200` and result fields.
4. No map → `200` without requiring confirmation.
5. Invalid dimensions/population → `400`.
6. After success, `GET /api/map` reports the new seed/dimensions and `HasMap == true`; `GET /api/data` reports the expected population.
7. `GET /api/simulation/status` still works and the engine is running.

Extend `Genelife.Api.IntegrationTests/Marten/ProjectionIntegrationTests.cs`:

- A second `MapGenerated` for `world` updates seed/width/height/version.
- `BuildingRemoved` removes the old row (already covered; add the regeneration scenario).

### 9.3 Admin E2E

Add a Playwright spec under `clients/browser/e2e/admin/` (there is currently no `map.spec.ts`):

1. Open `/map` with a running world.
2. Click **Create map**; assert the confirmation dialog is visible and mentions the map/buildings/NPCs/player replacement.
3. Click **Cancel**; assert no request was sent and the map is unchanged.
4. Click **Create map** → confirm; assert the spinner/disabled state, then the success message and updated map stats (seed/size/buildings).
5. With no map (simulation stopped), assert the dialog is skipped.

Use the existing admin helpers (`clients/browser/e2e/admin/family-api.ts`, `helpers.ts`) and local admin auth pattern.

### 9.4 Verification commands

From the repository root:

```powershell
dotnet build Genelife.Domain.Tests/Genelife.Domain.Tests.csproj
dotnet run --project Genelife.Domain.Tests/Genelife.Domain.Tests.csproj --no-build --no-launch-profile -- -trait- "Category=Scenario"
```

API integration (PostgreSQL required):

```powershell
docker compose up -d postgres
dotnet build Genelife.Api.IntegrationTests/Genelife.Api.IntegrationTests.csproj
dotnet run --project Genelife.Api.IntegrationTests/Genelife.Api.IntegrationTests.csproj --no-build --no-launch-profile --
```

Admin UI and browser checks:

```powershell
dotnet build Genelife.sln
cd clients/browser
npm ci
npm run typecheck
npm run lint
npm run test
npm run test:e2e:admin
```

Manual smoke:

1. `dotnet run --project GeneLife.AppHost`.
2. Open the admin UI on port `5068`, go to `/map`.
3. With a running world, create a map; confirm; verify the seed/size/buildings/NPC counts and the player marker.
4. Stop the simulation, create a map again, and verify no confirmation is needed.
5. Restart the API and verify the freshly saved world is restored.

---

## 10. Implementation order (PR-sized)

1. **Contracts + engine primitives**
   - `MapStatus` fields (`HasMap`, `Seed`, `LayoutVersion`).
   - `CreateMapRequest`/`CreateMapResult`.
   - `Initialize(startTick)`, `GeneratePopulation(...)`, `ClearTransientState()`.
2. **Manager orchestration + endpoint**
   - `SimulationManager.CreateMap` with `lifecycleGate`.
   - `POST /api/map/create` with validation and `409` confirmation.
   - Unit and API tests.
3. **Replication and projections**
   - `MapProjection` metadata refresh.
   - `SnapshotBroadcastService` full map snapshot + reset notification.
   - Old building document cleanup.
4. **Admin UI**
   - `GameDataService.CreateMapAsync` (and long-running timeout handling).
   - `ConfirmDialog.razor` component.
   - `/map` settings/action, dialog, success/error states.
5. **Live client resync (if live players are in scope)**
   - `WorldReset` event, `RequestWelcome` hub method, browser store reset handling.
   - If deferred, add a clear warning/reconnect requirement to the dialog and docs.
6. **E2E and docs**
   - Admin Playwright spec.
   - Update `tools/preview/reset.sh` to call `/api/map/create` (optional simplification).
   - Update the admin/README documentation if the operation is user-facing.

---

## 11. Open questions / decisions to confirm

1. **Population policy**: Should "create map" always regenerate the NPC population, or should it support a map-only reset (no NPCs)? This plan fills the population by default and exposes `Population = 0` as an explicit map-only option.
2. **Manual entities**: Confirm that discarding admin-created companies/sims is acceptable. If not, the feature needs a "re-place existing entities" mode, which is significantly more complex because placement can fail on the new layout.
3. **Tick semantics**: This plan preserves tick monotonicity for replication compatibility (the new world starts at the old tick + 1). If the product needs a true year-0 reset, add a world epoch to snapshots and reset the client/broadcast cursors instead.
4. **Live client handling**: Is the browser client expected to stay connected through a map reset? If yes, implement `WorldReset`/`RequestWelcome`; if no, require a page reload and document it.
5. **Map tuning**: Should the admin UI expose water fraction, road spacing, and zone shares now, or keep defaults for v1? This plan keeps defaults and can extend `CreateMapRequest` later.
6. **Map id retention**: This plan keeps `MapId = "world"` and bumps the layout version. If multiple named maps are a near-term goal, switch to a per-generation `MapId` and add cleanup for old `MapDocument` rows.

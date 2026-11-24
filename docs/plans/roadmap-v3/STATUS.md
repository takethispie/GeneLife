# Roadmap V3 — Status

**Status:** Implemented (V3 vertical slice). V5 feature 05 completed the deferred touch/audio, live smoke and prediction decision; the 30-minute preview soak remains.
**Builds on:** V2 map, placement, movement and replication
**Scope:** browser 2D client, PixiJS rendering, SignalR presence, viewport, input, HUD and release
**Client:** `clients/browser`

## Starting point

The server and contracts needed by a browser client were already in place. The
client-blocking gaps listed in the original plan are now resolved server-side.

| Area | Status | Evidence |
|---|---|---|
| Map contract | Implemented | `Genelife.Contracts/Dtos/MapSnapshot.cs`, `MapDelta.cs`, `MapStatus.cs` |
| Map transport | Implemented | `WorldHub.SendMapSnapshot`, `RequestMapSnapshot`, `/api/map/snapshot` |
| Dynamic replication | Implemented | `SendSnapshot`, `SendSnapshotDelta`, `SnapshotHistory`, reconnect tests |
| Viewport | Implemented | `SetViewport`, `ClearViewport`, `ViewportResult`, `ViewportRegistry` |
| Reconnect and invalidation | Implemented | `RequestSnapshot`, `AcknowledgeSnapshot`, force-full tracker |
| Player auth | Implemented | `POST /api/auth/token`, JWT, `access_token` on `/hubs` |
| Intent contract | Implemented | `Genelife.Contracts/Intents/Intent.cs` |
| Intent admission | Implemented | `WorldHub.SubmitIntent`, queue drain, `IntentResult` |
| Contracts assembly | Implemented | `Genelife.Contracts`, referenced by API, Web and tests |
| Player identity | Implemented in V3 | `WelcomeMessage` on connect with own `SimNetworkId` |
| Post-admission rejection | Implemented in V3 | `IntentRejected` drained from the engine and delivered by `SnapshotBroadcastService` |
| Player sim creation | Implemented in V3 | `SimulationEngine.EnsurePlayerSim`, auto-run on connect |
| Browser CORS | Implemented in V3 | Explicit origins plus `AllowCredentials()` for SignalR negotiate |
| Aspire dev stack | Implemented in V3 | `GeneLife.AppHost` runs the Vite client, injects `VITE_API_BASE_URL` and pins port 5173 |

## Completed in V3

### Foundation

- `clients/browser` TypeScript + Vite workspace, strict compiler settings, ESLint,
  Prettier, Vitest and Playwright.
- Runtime config validation (`src/config/env.ts`); API/hub URL derivation; build
  stamp and protocol version.
- Auth client and session controller with JWT parsing, expiry margin, refresh,
  sign-out and same-tab `sessionStorage` recovery.
- SignalR client with ordered handler registration, automatic reconnect,
  acknowledged snapshots, snapshot/map requests, viewport and intent calls.
- Loading screen with config/auth/assets/scene steps, visible failure and retry.
- Hub DTOs mirrored from `Genelife.Contracts`, structural runtime validators and
  deterministic fixtures for every payload plus malformed cases.
- Aspire AppHost integration: the Vite dev server runs as the `browser` resource,
  receives `VITE_API_BASE_URL` from the API's external endpoint, and is pinned to
  `http://localhost:5173` for the CORS allowlist.

### World rendering

- Per-row RLE decoder into a dense `Uint8Array` with fail-closed validation for
  gaps, overlaps, out-of-bounds runs and unknown flags.
- Map cache keyed by `MapId` + `Version`; building deltas apply in place and gaps
  force a full map refresh.
- Pixi scene with one world container and layered terrain, roads, buildings, sims,
  selection and effects.
- Camera in world units with clamping, pointer-anchored zoom, easing and reduced
  motion; pooled sprites culled to the visible tile rectangle.
- Tile variants, road masks, building footprints, roofs, entry markers and
  close-zoom labels.

### Realtime presence

- Mutable `SimulationStore` separate from UI state: atomic full snapshots, ordered
  deltas, removals, stale rejection, gap recovery and map-staleness pausing.
- Tick-indexed frame buffer with bounded interpolation delay, snap thresholds and
  no extrapolation past the newest frame.
- Debounced viewport synchronisation that never starves while idle, treats
  `ViewportResult` as authoritative and re-syncs after reconnect.
- Recovery order: auth, map freshness, then `RequestSnapshot(since | null)`.
- Bounded backoff with jitter for retries; receive-rate accounting.

### Input and HUD

- Absolute keyboard/pointer camera controls, wheel zoom, middle/Alt drag pan and
  canvas focus handling.
- Click-to-move and hover targeting with local walkability hints; every intent is
  still checked by the server.
- Monotonic intent sequence persisted for the tab, local rate limit, tile-only
  payloads and typed admission/async rejection feedback.
- Building selection and `UseBuildingIntent`; proximity is a hint only and range
  errors come back from the server.
- DOM HUD at 10 Hz: connection, player, own sim, map/version, tick, FPS, frame,
  visible sims, viewport radius, buffer, interpolation, rate, reconnect count,
  build and last error.
- Notifications with dedupe and a dismiss action, assertive live region, settings
  (reduced motion, HUD scale, debug, rebindable keys), focus rings and
  high-contrast/reduced-transparency media queries.
- Debug overlay with the frame and replication counters; close-zoom labels and
  entry markers.

### Hardening

- 25 Vitest files covering contracts, fixtures, RLE, map cache, store, deltas,
  interpolation, viewport, intent sequencing, recovery, auth, settings, HUD state,
  targeting, camera and performance budgets.
- Fixture Playwright smoke suite and live acceptance suite (connect/reload, two
  tabs, network interruption recovery).
- Client CI job with dependency audit, typecheck, lint, coverage, build and fixture Playwright.
- Soak script (`npm run soak`) for long-running connection/tick checks.
- Performance, security, deployment and release documents; static production
  build passes.

## Verification

| Check | Result |
|---|---|
| `dotnet build GeneLife.sln` | 0 errors |
| Full server suite | 358 total, 0 failed, 2 skipped |
| `npm run typecheck` | passing |
| `npm run lint` | passing |
| `npm run test` | 25 files, 148 tests passing |
| `npm run test:coverage` | statements 87.8%, branches 77.4%, lines 91.4% |
| `npm run build` | passing; ~125 KiB gzip main bundle |
| Fixture Playwright | 3/3 passing |
| Live Playwright | connect/reload, two tabs and network interruption recovery passing |
| Server hub tests | 13/13, including `Welcome` and `IntentRejected` |

## Decisions

| Decision | Choice |
|---|---|
| Client location | `clients/browser` in this repository with its own CI job |
| Token persistence | Memory plus `sessionStorage` for same-tab reload; documented XSS tradeoff |
| Map gap policy | Refresh the full map and pause dynamic apply while stale |
| Interpolation | 120 ms delay, three-tile snap threshold, three-tick maximum gap |
| Viewport | 200 ms debounce, one-tile threshold, server-clamped result |
| HUD framework | DOM/CSS only; simulation store stays framework-free |
| Player sim | Auto-create on connect when the world is running; prefer a living owned sim |
| Rejected intents | Admission result plus asynchronous `IntentRejected` with sequence and code |

## Known gaps and deferred work

- Own-player prediction is not implemented. V5 measured input-to-ack p95 at 51 ms locally and recorded a no-go decision; movement remains interpolation only.
- Touch controls, chat, social and player accounts UI exist; matchmaking is still out of scope.
- Audio is a synthesized cue set with mute/per-category/volume settings; there are no audio assets.
- The world itself is not screen-reader described; the HUD describes state,
  selection and errors.
- Silent network loss is detected by the server ping timeout, so automatic
  recovery can take tens of seconds. The live Playwright test accounts for this.
- Fixture mode is read-only and requires the Vite dev server for fixture files.
- The full 2,000-sim render path is budgeted at the data/interpolation layer;
  GPU frame rate is environment-dependent and documented in
  `clients/browser/docs/performance.md`.

## Next

1. Run the 30-minute live preview soak and record HUD input-ack p95 and the
   API metrics snapshot.
2. Capture representative hardware GPU frame numbers and tune sprite batching if
   needed.
3. Revisit prediction only if preview p95 reaches 100 ms or corrections exceed
   5% (see the V5 prediction decision).

# GeneLife browser client

The V3 browser client renders the deterministic GeneLife map with PixiJS, keeps a
short interpolation buffer of server snapshots, and submits server-authoritative
intents. It is a plain TypeScript + Vite application; the HUD is DOM/CSS and the
simulation store is never owned by a UI framework.

## Status

Implemented: foundation, world rendering, realtime presence, input/HUD and the
hardening deliverables in `docs/plans/roadmap-v3`.

Server prerequisites completed alongside the client:

- `Welcome` is sent on connect with the player's `simNetworkId`, tick interval,
  protocol version and map version.
- Post-admission intent rejections are delivered as `IntentRejected` events.
- A new browser player gets a controllable sim on connect
  (`SignalR:AutoCreatePlayerSim`, default `true`).
- CORS allows credentials for SignalR negotiate while keeping an explicit origin
  allowlist.

## Prerequisites

- Node.js 26+ and npm.
- The API on `http://localhost:5288` (Dev profile) and PostgreSQL, or fixture
  mode with no server.

```bash
# from the repository root
docker compose up -d postgres
dotnet run --project Genelife.API --launch-profile http
```

Start the simulation once with an admin token:

```bash
TOKEN=$(curl -s -X POST http://localhost:5288/api/auth/local-admin \
  | node -pe 'JSON.parse(require("fs").readFileSync(0,"utf8")).accessToken')
curl -s -X POST http://localhost:5288/api/simulation/start \
  -H "Authorization: Bearer $TOKEN"
```

## Run

### With Aspire (recommended)

From the repository root:

```bash
dotnet run --project GeneLife.AppHost
```

Aspire starts PostgreSQL, the API, the web app and this client. The client runs
on `http://localhost:5173`, and the AppHost injects `VITE_API_BASE_URL` from the
API's external endpoint, so no `.env.local` is needed. The pinned port also keeps
the origin in the API's CORS allowlist.

### Standalone

```bash
cd clients/browser
npm ci
cp .env.example .env.local       # API URL, log level, dev player prefix
npm run dev
```

Open `http://localhost:5173`. A per-tab player id is generated and stored in
`sessionStorage`; override it with `?playerId=alice`.

### Fixture mode

`http://localhost:5173/?fixtures=1` loads the recorded contract fixtures without
an API. Fixture mode is read-only: clicking a walkable tile reports that there is
no intent endpoint. The fixture JSON is served by the Vite dev server only.

## Configuration

| Variable | Purpose | Default |
|---|---|---|
| `VITE_API_BASE_URL` | REST base URL | required unless fixtures |
| `VITE_HUB_URL` | Hub URL | `${VITE_API_BASE_URL}/hubs/world` |
| `VITE_LOG_LEVEL` | `debug`, `info`, `warn`, `error` | `info` |
| `VITE_USE_FIXTURES` | Load local fixtures | `false` |
| `VITE_DEV_PLAYER_ID` | Prefix for generated dev ids | `dev-player` |
| `VITE_VIEWPORT_RADIUS` | Requested viewport radius | `32` |

Vite inlines these at build time. The API origin must be listed in
`Cors:AllowedOrigins`, and the hub origin must allow credentials.

## Commands

```bash
npm run typecheck
npm run lint
npm run test
npm run test:coverage
npm run build
npm run fixtures:generate
npm run contracts:rest          # needs a running API + Swagger
npm run test:e2e
npm run soak -- --minutes=30   # needs a live stack + simulation
```

### End-to-end

Fixture smoke tests run against a Vite dev server:

```bash
npm run dev -- --host 127.0.0.1
E2E_BASE_URL=http://127.0.0.1:5173 npx playwright test e2e/fixture-smoke.spec.ts
```

Live acceptance tests require the API, PostgreSQL and a running simulation:

```bash
VITE_API_BASE_URL=http://localhost:5288 npm run dev -- --host 127.0.0.1
E2E_LIVE=1 \
E2E_BASE_URL=http://127.0.0.1:5173 \
E2E_API_BASE_URL=http://localhost:5288 \
npx playwright test e2e/live-acceptance.spec.ts
```

The network-interruption test takes about a minute: the browser notices the dead
socket after the server ping timeout, then SignalR reconnects with the configured
backoff.

## Architecture

```text
src/
  app/          bootstrap, loading screen, client wiring
  auth/         token request, session lifecycle, optional sessionStorage
  config/       env validation, constants, build stamp
  contracts/    hub DTOs, runtime validators, REST types
  net/          SignalR client, intent sender, viewport debounce, recovery, traffic
  simulation/   mutable replication store, map cache/RLE decoder, interpolation
  render/       Pixi application, camera, culling, tile/building/sim layers
  input/        input map, camera input, targeting, building selection
  ui/           DOM HUD, notifications, settings, accessibility CSS
  test/         fixtures, scenario and performance tests
```

Rules enforced by the code:

- One Pixi application and one world container; the camera transforms that
  container. Sprites pool and cull by the visible tile rectangle.
- Network callbacks only mutate the store; Pixi objects are created and moved in
  the render tick.
- Snapshot gaps and map-version gaps request full state instead of guessing.
- Intents carry tiles and a monotonic sequence; no float positions are sent.
- The HUD coalesces updates at 10 Hz and renders all server strings with
  `textContent`.
- Tokens are never logged and never placed in `localStorage`.

## Server contract additions

| Event | Payload | Purpose |
|---|---|---|
| `Welcome` | `WelcomeMessage` | own sim id, tick interval, protocol/map version |
| `IntentRejected` | `IntentRejected` | sequence, code and message after admission |

Fixtures for both, plus malformed cases, live in `src/test/fixtures` and are
validated by `src/contracts/validate.ts`. Regenerate them with
`npm run fixtures:generate`.

## Troubleshooting

- **`VITE_API_BASE_URL is required`** — copy `.env.example` to `.env.local`, or
  open `/?fixtures=1`.
- **Auth succeeds but SignalR negotiate fails** — the API CORS policy must allow
  the browser origin *and* credentials. Restart the API after changing CORS.
- **`Your sim is not ready yet`** — the simulation is not running or the player
  sim has not been created yet. Start the simulation as an admin.
- **No map** — check the hub console log and the `Last error` HUD row; protocol
  or contract mismatches fail loudly instead of rendering partial state.

See `docs/security.md`, `docs/performance.md`, `docs/deploy.md` and
[`RELEASE.md`](RELEASE.md).

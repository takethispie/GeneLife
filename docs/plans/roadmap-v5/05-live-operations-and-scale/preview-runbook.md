# V5 preview runbook

The preview is one world, one API and one browser client. It is seeded from a
fixed world seed and a fixed population seed so operators can reproduce a run.

## Start the stack

```bash
docker compose up -d postgres
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5288 \
  dotnet run --project Genelife.API --no-launch-profile

cd clients/browser
VITE_API_BASE_URL=http://localhost:5288 npm run dev -- --host 127.0.0.1
```

`GeneLife.AppHost` does the same with Aspire and injects the API URL into Vite.

## Seed or reset

```bash
PREVIEW_API=http://localhost:5288 \
PREVIEW_SEED=21 PREVIEW_SIMS=500 \
  tools/preview/reset.sh
```

The script obtains a local-admin token, stops the simulation, starts a fresh map,
generates the population from the fixed seed and saves a snapshot. It prints the
world population (from `/api/data`, not the Marten history) and fails if the
population never reaches the requested count.

`PREVIEW_API`, `PREVIEW_SEED`, `PREVIEW_SIMS`, `PREVIEW_ADULTS` and
`PREVIEW_CHILDREN` are the supported knobs. Defaults are 21, 500, 380 and 120.

## Preview acceptance

Browser player, appearance, consumption and social read models:

```bash
cd clients/browser
E2E_LIVE=1 E2E_API_BASE_URL=http://localhost:5288 \
  npx playwright test e2e/live-acceptance.spec.ts --project=chromium
```

The `live V5 release smoke` group checks the read models and records the
input-to-ack latency shown in the HUD.

Admin Blazor pages (family, social, map, appearance, consumption):

```bash
cd clients/browser
E2E_ADMIN_LIVE=1 E2E_ADMIN_SERVER_URL=http://localhost:5068 \
  npx playwright test --config playwright.admin.config.ts
```

## Observability

- API health: `GET /health`
- Admin operational snapshot: `GET /api/simulation/metrics` (admin token), output
  includes tick rate, overrun/dropped counts, p95, GC counters, entity counts and
  the scaling-trigger evaluation.
- Browser: HUD connection/tick/buffer/input-ack, debug overlay (`F3`) for
  camera, bytes per second, buffer and build stamp.
- Logs: API log lines include `[metrics]` every 250 ticks with tick p50/p95/p99,
  allocation, GC and top systems.

Thresholds used by the release gate:

| Signal | Healthy | Act |
|---|---|---|
| Tick p95 at 2k sims | ≤ 15 ms guardrail, ≤ 100 ms acceptance cap | profile or reduce cadence |
| Tick p95 at 32 players | ≤ 100 ms | reduce social work or tick rate |
| Input-to-ack p95 | < 100 ms | revisit prediction (see `prediction-decision.md`) |
| Health failures in status | 0 | block release until fixed |
| Money conservation gap | 0 | block release until fixed |

## Manual preview script

1. Open the client, walk and select a building, use one venue.
2. Open the admin map, relocate an occupied building; confirm residents,
   employees, students and the venue move with it.
3. Open family/admin pages and confirm the recent actions appear.
4. Save, stop and restore; confirm the population returns.
5. Record the HUD input-ack p95 and `/api/simulation/metrics` values in the
   release evidence.

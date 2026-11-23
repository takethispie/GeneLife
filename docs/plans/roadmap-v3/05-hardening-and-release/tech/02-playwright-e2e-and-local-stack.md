# H02 — Playwright E2E and local stack

**Phase:** 2 — Verification
**Depends:** H01, I06
**Estimate:** 2 sessions
**Status:** Planned (V3)

## Goal

Automate the real path: browser → auth → hub → map → snapshot → move → reconnect, against a local or preview API.

## Context

Unit tests cannot prove CORS, SignalR auth, browser focus, WebGL boot or HUD wiring. Playwright can, and it should use the same player flow as a real user.

## Design

### Local stack

```bash
docker compose up -d postgres
dotnet run --project Genelife.API
npm --prefix clients/browser run dev
```

Seed a deterministic world with the existing simulation/admin endpoints. Use a dedicated test player and clean schema.

### Scenarios

1. Auth and connect.
2. First map and own sim.
3. Move to a walkable tile.
4. Blocked target shows feedback and does not move.
5. Select building, interact, observe result.
6. Force a socket drop, assert reconnect.
7. Reload the page and resume.

### Rules

- No production API in CI.
- No fixed sleeps where a UI signal exists.
- Capture screenshot and console/network logs on failure.
- Use a fresh player id per test.
- Keep the local stack startup scripted.

## Tasks

- [ ] Add `e2e` Playwright config and local-stack script.
- [ ] Add auth/connect/map smoke test.
- [ ] Add move and blocked-target tests.
- [ ] Add interaction test.
- [ ] Add reconnect/reload test.
- [ ] Add CI job with Postgres and API.
- [ ] Add failure artifacts.

## Files

- new `clients/browser/playwright.config.ts`
- new `clients/browser/e2e/*.spec.ts`
- new `clients/browser/e2e/global-setup.ts`
- new `scripts/start-client-e2e.sh` (or platform equivalent)

## Acceptance

- E2E passes from a clean checkout.
- A CORS/auth regression fails E2E.
- A reconnect regression fails E2E.
- Failures include screenshot and logs.
- The suite runs in CI with the server.

## Testing

- CI E2E job.
- Manual local run.
- Failure-artifact check.

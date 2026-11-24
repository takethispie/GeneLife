# L04 — Live preview and browser acceptance

**Phase:** 4 — Release
**Depends:** L01–L03, features 01–04
**Estimate:** 2–3 sessions
**Status:** Done locally (V5 feature 05); 30-minute soak and hardware GPU numbers remain

## Goal

Deploy a seeded preview of V5 and prove the real browser, auth, hub, database and admin pages work together outside the test harness.

## Context

V3 has fixture and live Playwright suites but the live acceptance against a seeded preview is still open. V5 adds player actions, appearance, utilities, grocery, social and admin pages that need a deployed smoke test.

A local unit test cannot catch CORS, token expiry, proxy, asset path or database migration problems in a real deployment.

## Design

### Preview environment

- A seeded world with a known seed, 200–2,000 sims, utilities, grocery, families and a few social venues.
- The API, PostgreSQL, admin web and browser client deployed behind the configured origins.
- Admin and player test accounts with separate tokens.
- A reset script that restores the seeded world without manual steps.
- A migration rehearsal on a copy of the previous schema.

### Live acceptance

- Player: connect, see the map, control a sim, get a job, rent a home, buy food, form a family.
- Appearance: see distinct sims, family resemblance and age bands.
- Consumption: observe a bill, an outage and a grocery purchase.
- Social: two players see each other, chat, friend, block and report.
- Admin: open `/player`, `/appearance`, `/consumption`, `/social` and `/family`.
- Reconnect: drop the network and recover without ghost sprites or lost state.
- Rollback: restore the previous build and the pre-migration snapshot.

### Observability

- Capture tick p95/p99, frame time, payload size and connection count.
- Capture hub reconnect count and rejected intent rate.
- Capture projection lag and admin page latency.
- Record the hardware and browser used for the render numbers.
- Alert on invariant failures, tick overrun and projection lag.

### Evidence

- Playwright report and screenshots.
- Server logs with the seeded run id.
- Performance JSON.
- Migration and rollback transcript.
- A short release note with known gaps.

## Tasks

- [x] Add the seeded preview configuration and reset script.
- [x] Deploy API, database, admin and browser client.
- [x] Add the live Playwright suite for player, appearance, consumption and social.
- [x] Add the admin page smoke suite.
- [x] Capture performance and render numbers.
- [x] Add monitoring and alert thresholds.
- [ ] Rehearse migration and rollback.
- [x] Record the evidence bundle.

## Resolution

The preview ran locally with PostgreSQL, the API, the Blazor admin and the Vite
client. `tools/preview/reset.sh` seeds from a fixed map/population seed and prints
the world population from `/api/data`. Player and admin live smokes passed;
reconnect recovered; the HUD and `/api/simulation/metrics` supplied the numbers.
GPU frame time, the 30-minute soak and the migration/rollback rehearsal remain
exceptions in the release evidence.

## Files

- `.github/workflows/preview.yml` or the existing deployment workflow
- `clients/browser/e2e/live/*`
- `clients/browser/docs/performance.md`
- `docker-compose.yml`
- `docs/plans/roadmap-v5/05-live-operations-and-scale/release-evidence.md`

## Acceptance

- The preview starts from a clean seed and is reachable from the browser client.
- Player, appearance, consumption, social and admin flows pass live.
- Reconnect recovers without a half-applied state.
- Performance and render numbers are recorded with hardware/browser.
- Migration and rollback are rehearsed.
- Monitoring alerts on the defined thresholds.
- The evidence bundle is complete enough for a release decision.

## Testing

- Run the live Playwright suite.
- Run the admin smoke suite.
- Run a 30-minute soak with multiple players.
- Run the migration and rollback rehearsal.
- Compare the preview tick/frame numbers to the local budget.

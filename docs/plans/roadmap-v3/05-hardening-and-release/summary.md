# Hardening and Release — Summary

**Folder:** `05-hardening-and-release/`
**Status:** Planned (V3)

**Depends on:** foundation, rendering, realtime presence, input/HUD
**Feeds:** first public browser playtest and subsequent client roadmaps

## Objective

Make the browser client repeatable and operable: deterministic fixtures, unit and Playwright coverage, measured render and payload budgets, diagnostics that do not leak secrets, a safe static deployment and a release checklist that can be repeated.

## Baseline before V3

- The server suite is green; the client has no tests or CI yet.
- The client will have multiple stateful layers: auth, connection, replication, interpolation, input and HUD.
- The target scale is 2,000 NPCs, a 256×256 map and 32 players.
- The existing `ScalingTriggers` and replication metrics provide server-side signals.
- No client performance or error telemetry exists.

## Deliverables

1. Vitest coverage for RLE decoding, map caching, delta application, interpolation and intent sequencing.
2. JSON fixtures captured from real server contracts and checked into the client.
3. A Playwright suite against a local API + seeded world.
4. Render-budget and payload-budget checks at the target scale.
5. Client diagnostics: frame time, FPS, entity counts, bytes, interpolation delay, reconnects, rejected intents.
6. Security review for token handling, CORS, XSS, dependencies and rate-limit behaviour.
7. Static build, preview deployment, version stamp and rollback procedure.
8. A release checklist and a short post-playtest feedback loop.

## Architecture

```text
Unit fixtures ──► store/decoder tests
Playwright ─────► real API + seeded world ──► browser client
Telemetry ──────► local diagnostics + optional sink
Static build ───► CDN/host ──► API base URL per environment
```

Testing does not bypass the server contract. The Playwright path exercises the same auth, hub and intent flow as a player.

## Tech steps

| # | Step | Phase |
|---|---|---|
| 01 | [Unit, contract and scenario tests](tech/01-unit-contract-and-scenario-tests.md) | Verification |
| 02 | [Playwright E2E and local stack](tech/02-playwright-e2e-and-local-stack.md) | Verification |
| 03 | [Performance, soak and observability](tech/03-performance-soak-and-observability.md) | Quality |
| 04 | [Security, abuse and versioning](tech/04-security-abuse-and-versioning.md) | Safety |
| 05 | [Build, deploy and rollback](tech/05-build-deploy-and-rollback.md) | Release |
| 06 | [Release acceptance](tech/06-release-acceptance.md) | Verification |

## Business reading

- [Quality and operations](business/01-quality-and-operations.md)
- [Release and learning](business/02-release-and-learning.md)

## Definition of done

- Unit and fixture tests cover every decoder/store/interpolation edge that can corrupt state.
- Playwright proves auth → connect → map → move → interact → reconnect.
- The client holds its render and payload budgets at 2,000 sims on the agreed reference machine.
- Diagnostics identify the failing layer without exposing tokens or player PII.
- A production build deploys from CI, stamps its version and can be rolled back.
- Release notes list known limitations, server compatibility and the next client roadmap.

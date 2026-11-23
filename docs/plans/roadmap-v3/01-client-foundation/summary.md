# Client Foundation — Summary

**Folder:** `01-client-foundation/`
**Status:** Planned (V3)

**Depends on:** V2 contracts, `Genelife.Contracts`, `/api/auth/token`, `/hubs/world`, existing CORS configuration
**Feeds:** world rendering, realtime presence, input/HUD and release

## Objective

Create a browser workspace that can boot, authenticate, connect, load contract types and fixtures, and render an empty Pixi scene. Everything after this feature assumes a repeatable local build and a typed boundary against the server.

## Baseline before V3

- No browser client exists.
- `Genelife.Contracts` is a `net10.0` assembly; JavaScript has no generated types yet.
- The API exposes Swagger/OpenAPI but the hub methods and SignalR payloads are not described by OpenAPI.
- `POST /api/auth/token` issues a player JWT.
- `Cors:AllowedOrigins` currently lists local admin/debug origins only.
- PixiJS, Vite and SignalR are not dependencies anywhere in the repository.

## Deliverables

1. A `clients/browser` TypeScript workspace with Vite, typecheck, lint, test and build scripts.
2. A typed contract boundary: generated REST types plus hand-maintained hub DTO types and JSON fixtures.
3. A player auth/session flow that obtains a JWT, tracks expiry and connects to `/hubs/world`.
4. Runtime configuration for API/hub URLs, environment, logging level and feature flags.
5. PixiJS bootstrapping, an asset manifest and a placeholder scene.
6. A local development loop against the real API, with a recorded-fixture fallback.

## Architecture

```text
Vite env ──► config
                │
                ├──► REST client ──► /api/auth/token
                │                        │
                │                        ▼
                └──► session store ──► JWT + player id
                                         │
                                         ▼
                                   HubConnection
                                         │
                                         ▼
                                    empty Pixi scene
```

The foundation does not apply world state yet. Its job is to make the transport, auth, types, assets and build reproducible.

## Tech steps

| # | Step | Phase |
|---|---|---|
| 01 | [Browser workspace and tooling](tech/01-browser-workspace-and-tooling.md) | Foundation |
| 02 | [Contracts and fixtures](tech/02-contracts-and-fixtures.md) | Contract |
| 03 | [Auth, session and connection identity](tech/03-auth-session-and-connection-identity.md) | Identity |
| 04 | [Runtime config, assets and dev bootstrap](tech/04-runtime-config-assets-and-dev-bootstrap.md) | Runtime |
| 05 | [Foundation acceptance](tech/05-foundation-acceptance.md) | Verification |

## Business reading

- [Why browser first](business/01-why-browser-first.md)
- [Session, identity and trust](business/02-session-identity-and-trust.md)

## Definition of done

- `npm install && npm run dev` starts the client from a clean checkout.
- `npm run typecheck`, `npm run lint`, `npm run test` and `npm run build` pass.
- The client obtains a player token and connects to `/hubs/world` with it.
- The connection state is visible on screen and in logs.
- Contract fixtures fail loudly when the server shape changes.
- The Pixi canvas boots with an asset manifest and a placeholder scene.
- No secret, token or environment-specific URL is committed.

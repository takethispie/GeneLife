# F04 — Runtime config, assets and dev bootstrap

**Phase:** 4 — Runtime
**Depends:** F01, F03
**Estimate:** 1–2 sessions
**Status:** Planned (V3)

## Goal

Make the client configurable per environment, load its visual assets predictably, and boot into a visible placeholder scene against the real API or recorded fixtures.

## Context

The client needs different API/hub URLs in local, preview and production. It also needs a texture/tileset pipeline before map rendering can start. None of this should be hardcoded in source.

## Design

### Environment

```text
VITE_API_BASE_URL=http://localhost:5000
VITE_HUB_URL=http://localhost:5000/hubs/world
VITE_LOG_LEVEL=debug
VITE_USE_FIXTURES=false
```

- Parse and validate env at startup; fail fast with a readable error.
- Never put a JWT or secret in an env file.
- A build stamp is injected at build time.

### Asset manifest

```ts
type AssetManifest = {
  tileset: string;
  simAtlas: string;
  buildingAtlas: string;
  audio?: string[];
};
```

- Load assets before starting the render ticker.
- Fail with a visible error if a required asset is missing.
- Use hashed asset URLs in production.
- Keep the first asset set small: terrain, roads, one sim token, building silhouettes, selection marker.

### Dev bootstrap

- A player-id field or fixed local dev id.
- A “connect” button that authenticates and starts the hub.
- A fixture mode that loads `map-snapshot.json` without a server.
- A loading screen with explicit steps: config → auth → assets → scene.

## Tasks

- [ ] Add env parsing with startup validation.
- [ ] Add `.env.example` and document each variable.
- [ ] Add the asset manifest and preload step.
- [ ] Add a placeholder Pixi scene with a loading/error state.
- [ ] Add a dev player-id/connect control.
- [ ] Add fixture-mode startup.
- [ ] Add build-stamp injection.

## Files

- new `clients/browser/src/config/env.ts`
- new `clients/browser/src/assets/manifest.ts`
- new `clients/browser/src/assets/preload.ts`
- new `clients/browser/src/app/bootstrap.ts`
- new `clients/browser/.env.example`
- `clients/browser/vite.config.ts`

## Acceptance

- A missing API URL fails with a clear startup message.
- The app loads a placeholder canvas and asset manifest.
- Fixture mode boots without an API.
- The dev connect control reaches an authenticated hub state.
- Production build contains no secrets and includes a build stamp.

## Testing

- Env validation tests.
- Asset manifest load failure test.
- Bootstrap state transition test.
- Fixture-mode smoke test.

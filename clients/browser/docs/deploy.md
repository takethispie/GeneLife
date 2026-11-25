# Browser client deployment

## Build

```bash
cd clients/browser
npm ci
VITE_API_BASE_URL=https://api.example.com \
VITE_LOG_LEVEL=warn \
VITE_BUILD_STAMP="$GIT_SHA" \
npm run build
```

`npm run build` runs `tsc --noEmit` and then Vite. Output: `dist/` with hashed
assets and `index.html`.

Vite inlines `VITE_*` values at build time, so build one artifact per environment
(or use a runtime config file if that becomes a requirement).

## Hosting

- Serve `dist/` as static files over HTTPS.
- Cache hashed assets under `/assets/` immutably.
- Serve `index.html` with `no-cache` so a new deploy is picked up.
- Enable gzip/brotli; the main bundle is ~125 KiB gzipped plus a ~36 KiB chunk.
- SPA fallback is not needed: the client is a single route with query parameters.
- Do not rewrite `/hubs/*`; the hub lives on the API origin.

## API requirements

- Add the deployed client origin to `Cors:AllowedOrigins`.
- Keep `AllowCredentials()` with the explicit origin list for SignalR.
- Expose the API over HTTPS/WSS.
- Start the simulation and seed a world before announcing the client; a stopped
  simulation produces a visible "genos not ready" error.

## Build diagnostics

`buildInfo().stamp` comes from `VITE_BUILD_STAMP`, `GITHUB_SHA`, or the build time.
It is shown in the HUD and debug overlay so a screenshot identifies the bundle.

The HUD reports connection state, map id/version, tick, FPS, frame time, visible
genoses, viewport radius, buffer depth, interpolation delay, receive rate, reconnect
count and the last error. No token or player record is included.

## Rollback

1. Keep the previous immutable asset directory and `index.html`.
2. Roll back by pointing the host at the previous `index.html` (or redeploy the
   previous artifact).
3. Verify the build stamp in the HUD matches the rolled-back artifact.
4. Confirm the API protocol version still matches the client's
   `CLIENT_PROTOCOL_VERSION`. A mismatch fails closed with a visible error.

## Smoke checklist after deploy

- Load the site; the loading screen reaches "World scene".
- Connection shows `Connected`; the player's own genos is visible and ringed.
- Map id/version and tick are non-zero and the tick advances.
- Pan and zoom; after stopping, the viewport radius row updates.
- Click a walkable tile; the target marker appears and the server moves the genos.
- Click a building; the selection panel opens and `Interact` reports the server's
  answer.
- Force a bad target (blocked/out of range) and confirm a typed notification.
- Reload the page; the same player and genos resume.
- F3 shows the debug overlay with no errors.

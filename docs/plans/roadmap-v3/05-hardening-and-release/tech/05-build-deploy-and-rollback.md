# H05 — Build, deploy and rollback

**Phase:** 5 — Release
**Depends:** H02, H03, H04
**Estimate:** 1–2 sessions
**Status:** Planned (V3)

## Goal

Produce a repeatable static build, deploy it to a configured origin, connect it to the right API, and roll back without a code change.

## Context

The browser client is static assets plus environment configuration. There is no installer and no desktop update path, but there are cache, CORS and version hazards.

## Design

### Build

```bash
npm --prefix clients/browser ci
npm --prefix clients/browser run typecheck
npm --prefix clients/browser run lint
npm --prefix clients/browser run test
npm --prefix clients/browser run build
```

- Inject `VITE_API_BASE_URL`, `VITE_HUB_URL` and build stamp at CI time.
- Hashed assets with long cache headers; `index.html` no-cache.
- No source secrets.
- Optional source maps for preview, restricted for production.

### Deploy

- Static host/CDN or the existing web host.
- Add the deployed origin to API CORS.
- Smoke test auth, hub, map and snapshot.
- Keep the previous build available for rollback.

### Rollback

- Roll back by re-pointing the host to the previous asset set.
- Confirm API compatibility before rollback.
- Document which server contract version each client build expects.

## Tasks

- [ ] Add the CI build job and artifact.
- [ ] Add environment-specific config injection.
- [ ] Add static cache headers.
- [ ] Add deploy and smoke-test steps.
- [ ] Add rollback instructions and previous-build retention.
- [ ] Add CORS verification for the deployed origin.
- [ ] Add build stamp to the HUD/debug overlay.

## Files

- new `.github/workflows/client.yml` (or existing CI equivalent)
- new `clients/browser/docs/deploy.md`
- `clients/browser/vite.config.ts`

## Acceptance

- CI produces a deployable artifact.
- The deployed origin can auth, connect, load the map and receive snapshots.
- `index.html` is not permanently cached.
- A rollback restores the previous client.
- Build stamp and API URL are visible in diagnostics.

## Testing

- CI artifact build.
- Preview deploy smoke test.
- Cache-header check.
- Rollback drill.

# V3 browser client release checklist

Version: `0.1.0` · Client protocol: `1` · Roadmap: `docs/plans/roadmap-v3`

## Code and contracts

- [x] `clients/browser` workspace with TypeScript, Vite, PixiJS and SignalR.
- [x] Hub DTOs validated at runtime; recorded fixtures for every payload.
- [x] `Welcome` exposes the player's own genos, tick interval, protocol and map version.
- [x] Post-admission `IntentRejected` reaches the player's active connections.
- [x] Map snapshot/delta cache with version-gap recovery.
- [x] Snapshot/delta store with acknowledged ticks, gap recovery and stale rejection.
- [x] Tile/building/genos rendering, culling, pooling, camera, labels and entry markers.
- [x] Interpolation buffer with bounded delay and snap rules.
- [x] Debounced viewport sync that trusts `ViewportResult`.
- [x] Reconnect recovery with auth refresh and full-snapshot fallback.
- [x] Keyboard/pointer camera, click-to-move, hover/target markers.
- [x] Monotonic intent sequencing, local rate limit and typed feedback.
- [x] Building selection and `UseBuildingIntent`, range left to the server.
- [x] DOM HUD at 10 Hz, notifications, settings, reduced motion, debug overlay.
- [x] Loading screen, visible errors, retry and sign-out.

## Verification

Run from `clients/browser`:

```bash
npm ci
npm audit --omit=dev --audit-level=high
npm run typecheck
npm run lint
npm run test
npm run test:coverage
npm run build
```

Run from the repository root:

```bash
dotnet build GeneLife.sln -c Release
dotnet run --project Genelife.UnitTests -c Release --no-build --no-launch-profile
```

Playwright:

```bash
E2E_BASE_URL=http://127.0.0.1:5173 npx playwright test e2e/fixture-smoke.spec.ts
E2E_LIVE=1 E2E_BASE_URL=http://127.0.0.1:5173 E2E_API_BASE_URL=http://localhost:5288 \
  npx playwright test e2e/live-acceptance.spec.ts
npm run soak -- --minutes=30
```

Last local result:

- Client: 25 files / 148 tests passing; coverage statements 87.8%, branches 77.4%,
  lines 91.4%.
- Fixture Playwright: 3/3 passing.
- Live Playwright: connect/reload, two tabs, and a ~50 s network interruption
  recovery path.
- Server hub tests: 13/13 passing, including `Welcome` and `IntentRejected`.

## Performance

- Budgets and measured results: `clients/browser/docs/performance.md`.
- 2,000-genos interpolation, 2,000-genos snapshot apply and 256×256 decode are
  enforced in tests.
- Record the device, resolution and visible counts when adding new budget claims.

## Accessibility

- Canvas has an accessible label and is keyboard focusable.
- HUD is real DOM, tabbable, with visible focus rings and an assertive live region
  for errors.
- Reduced motion is honored from settings and `prefers-reduced-motion`.
- High-contrast and reduced-transparency media queries are included.
- Escape returns focus to the canvas; Enter activates the hovered target.
- Known gap: there is no screen-reader rendering of the world itself; the HUD
  describes connection, selection and errors.

## Deployment

Follow `clients/browser/docs/deploy.md`. Confirm:

- [ ] Deployed origin added to `Cors:AllowedOrigins`.
- [ ] HTTPS/WSS and credential-enabled CORS.
- [ ] Simulation running with a seeded world.
- [ ] Build stamp visible in the HUD.
- [ ] Rollback artifact and previous `index.html` retained.

## Known limitations

- No own-player prediction; movement visibly waits for server snapshots and
  interpolation.
- No mobile/touch control scheme; keyboard and pointer only.
- Fixture mode is read-only and served only by Vite.
- Automatic reconnect depends on the browser noticing a dead socket (server ping
  timeout), so recovery can take tens of seconds on a silent network loss.
- No audio cues beyond the settings placeholder.
- Network-drop E2E takes about a minute and is not part of the default PR job.

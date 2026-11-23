# F05 — Foundation acceptance

**Phase:** 5 — Verification
**Depends:** F01–F04
**Estimate:** 1 session
**Status:** Planned (V3)

## Goal

Prove the client foundation is reproducible, typed, authenticated and ready for map rendering.

## Context

This is the first stop point. If the workspace, contracts, auth, config or asset boot is fragile, every later feature inherits the problem.

## Design

Run the foundation in two modes:

1. **Live API mode** — real token, real hub connection, empty scene.
2. **Fixture mode** — recorded map fixture, no server, deterministic scene.

Both modes must show the same connection/session shell and fail visibly.

## Tasks

- [ ] Verify clean install and all scripts.
- [ ] Verify live auth and hub connection.
- [ ] Verify fixture mode.
- [ ] Verify CORS from the dev origin.
- [ ] Verify no token leaks in logs or build output.
- [ ] Record the foundation demo steps in the client README.

## Files

- `clients/browser/README.md`
- `clients/browser/src/app/bootstrap.ts`
- `clients/browser/src/auth/session.ts`

## Acceptance

- `npm install`, `npm run typecheck`, `npm run lint`, `npm run test` and `npm run build` pass from clean.
- A player can authenticate and reach `connected`.
- The placeholder scene renders in both live and fixture modes.
- A missing env var or asset produces a readable failure.
- No secret or environment-specific URL is committed.

## Testing

- Manual clean-checkout run.
- CI client job.
- A short Playwright smoke test for the shell is optional here and mandatory in V3 H05.

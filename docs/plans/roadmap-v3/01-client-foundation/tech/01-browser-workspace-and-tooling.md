# F01 — Browser workspace and tooling

**Phase:** 1 — Foundation
**Depends:** none
**Estimate:** 1 session
**Status:** Planned (V3)

## Goal

Create a standalone TypeScript browser client at `clients/browser/` with a reproducible install, typecheck, lint, unit-test and production-build loop.

## Context

The repository contains a .NET API and a Blazor admin app. Neither is the player client. The browser client should be a separate Vite application so its dependencies, build and release cadence do not leak into `Genelife.Web`.

## Design

```text
clients/browser/
  package.json
  tsconfig.json
  vite.config.ts
  eslint.config.js
  .env.example
  src/
  public/
```

Recommended dependencies:

- `pixi.js`
- `@microsoft/signalr`
- dev: `typescript`, `vite`, `vitest`, `eslint`, `prettier`, `@playwright/test`

Scripts:

```json
{
  "scripts": {
    "dev": "vite",
    "build": "tsc --noEmit && vite build",
    "typecheck": "tsc --noEmit",
    "lint": "eslint .",
    "test": "vitest run",
    "test:e2e": "playwright test"
  }
}
```

Rules:

- No React, Redux or state framework in V3.
- Strict TypeScript (`strict`, `noUncheckedIndexedAccess`, `exactOptionalPropertyTypes` where practical).
- No path aliases that hide dependency direction; `src` folders are explicit.
- CI runs install, typecheck, lint, unit tests and build.

## Tasks

- [ ] Create `clients/browser` and initialize the Vite TypeScript project.
- [ ] Pin the Node version and package manager.
- [ ] Add strict TypeScript, ESLint, Prettier and Vitest.
- [ ] Add `dev`, `build`, `typecheck`, `lint`, `test` and `test:e2e` scripts.
- [ ] Add a minimal `index.html` and `src/app/main.ts` entry point.
- [ ] Add a CI job that runs the client checks independently from .NET.
- [ ] Document local startup in the client README.

## Files

- new `clients/browser/package.json`
- new `clients/browser/tsconfig.json`
- new `clients/browser/vite.config.ts`
- new `clients/browser/eslint.config.js`
- new `clients/browser/src/app/main.ts`
- new `clients/browser/README.md`

## Acceptance

- `npm install` works from a clean checkout.
- `npm run dev` serves the app.
- `npm run typecheck`, `npm run lint`, `npm run test` and `npm run build` pass.
- CI fails when any client check fails.
- No client dependency is added to `Genelife.Web` or the .NET solution.

## Testing

- A trivial unit test proves Vitest is wired.
- A production build produces hashed assets.
- The client CI job runs on the same commit as the server build.

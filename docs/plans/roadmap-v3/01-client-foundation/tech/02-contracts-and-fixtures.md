# F02 — Contracts and fixtures

**Phase:** 2 — Contract
**Depends:** F01
**Estimate:** 1–2 sessions
**Status:** Planned (V3)

## Goal

Give the client a typed, testable boundary for REST and SignalR payloads without duplicating the server contract by memory.

## Context

REST endpoints are described by Swagger/OpenAPI, but hub methods and SignalR events are not. `Genelife.Contracts` is the source of truth for map, snapshot, viewport and intent shapes. The client needs both generated REST types and hand-maintained hub types that fail loudly when the server shape changes.

## Design

### REST

Generate types from the running API’s OpenAPI document:

```bash
npx openapi-typescript http://localhost:5000/swagger/v1/swagger.json -o src/contracts/rest.ts
```

Use generated types for auth and any REST read models. Do not hand-write REST response types.

### Hub DTOs

Mirror these contracts in `src/contracts/hub.ts`:

- `MapSnapshot`, `MapTileRun`
- `MapBuildingStatus`, `MapDelta`
- `WorldSnapshot`, `WorldSnapshotDelta`
- `SimStatus`, `CompanyStatus`
- `SetViewportRequest`, `ViewportResult`
- `Intent` (`type` discriminator: `move`, `useBuilding`)
- `IntentResult`, `SnapshotRejected`

JSON is camelCase. The `Intent` discriminator is the `type` property. `MapTileRun.Flags` is a comma-separated string such as `Land, Road`.

### Fixtures

Capture real payloads from the server and store them under:

```text
src/test/fixtures/
  map-snapshot.json
  world-snapshot.json
  world-delta.json
  map-delta.json
  intent-move.json
```

Fixture tests assert required fields, numeric bounds, RLE coverage and polymorphic intent parsing. A server contract change that breaks the client should fail a Vitest test before runtime.

## Tasks

- [ ] Add the OpenAPI type generation command and a committed generated file.
- [ ] Define `src/contracts/hub.ts` from `Genelife.Contracts`.
- [ ] Add JSON fixtures for map, snapshot, delta, map delta and intents.
- [ ] Add runtime validators at the network boundary for required fields.
- [ ] Add fixture tests for RLE coverage, camelCase parsing and intent discriminator.
- [ ] Document the rule that OpenAPI does not cover hub DTOs.
- [ ] Add a CI check that regenerating REST types produces no unexpected diff.

## Files

- new `clients/browser/src/contracts/rest.ts`
- new `clients/browser/src/contracts/hub.ts`
- new `clients/browser/src/contracts/validate.ts`
- new `clients/browser/src/test/fixtures/*.json`
- new `clients/browser/src/contracts/*.test.ts`

## Acceptance

- REST calls use generated types.
- Hub payloads parse into the mirrored types.
- A malformed RLE run fails a unit test.
- A `move` intent parses as `MoveIntent`; a `useBuilding` intent parses as `UseBuildingIntent`.
- A field rename in `Genelife.Contracts` is caught by a fixture or fixture-generation review.

## Testing

- Fixture round-trip tests.
- Discriminator tests.
- Unknown-field tolerance tests.
- Required-field rejection tests.

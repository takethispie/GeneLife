# L02 — Command, event and status reconciliation

**Phase:** 2 — Architecture
**Depends:** L01
**Estimate:** 1–2 sessions
**Status:** Planned (V5)

## Goal

Make the command/event path and the roadmap status documents agree with the code before V5 release.

## Context

`multiplayer-v1/README.md` still marks MP05 partial because command intents were not drained, but `SimulationEngine.DrainPlayerIntents()` now applies `MoveIntent` and `UseBuildingIntent`. Other roadmap status text may have drifted in the same way.

A stale status document is a real defect: it sends the next engineer to fix a problem that no longer exists or to trust a guarantee that was never implemented.

## Design

### Command audit

- Inventory every player and admin command, its admission path, its drain point and its result path.
- Assert every command is drained exactly once.
- Assert every command has an authorization check and an audit event.
- Assert no command mutates ECS state outside the tick lock.
- Assert no command is accepted but silently dropped.

### Event audit

- Inventory every durable event and its projection.
- Assert every durable event has an idempotent projection or an explicit reason not to.
- Assert event names and payloads match the docs.
- Assert chat and moderation documents are outside the world snapshot.

### Status reconciliation

Update:

- `docs/plans/multiplayer-v1/README.md` — MP05 status.
- `docs/plans/hybrid-wolverine/README.md` and `migration-checklist.md` — H16/H19/H20 status after L01.
- `docs/plans/performance-scaling/README.md` — any trigger-gated item that changed.
- `docs/plans/roadmap-v2/STATUS.md` — relocation and commute status after L03.
- `docs/plans/roadmap-v3/STATUS.md` — live preview, audio, touch and prediction status after L04–L06.
- `docs/plans/roadmap-v4/STATUS.md` — cohabitation status after feature 03.
- `docs/plans/roadmap-v5/STATUS.md` — V5 progress.

### Documentation check

Add a small script or test that fails when a status file references a code path that no longer exists, or when a command/event table is missing an entry.

## Tasks

- [ ] Inventory player and admin commands.
- [ ] Inventory durable events and projections.
- [ ] Assert drain, authorization and audit coverage.
- [ ] Update MP05 and the hybrid status docs.
- [ ] Update the V2, V3, V4 and V5 status docs.
- [ ] Add a docs/code consistency check.
- [ ] Add a command/event coverage test.
- [ ] Record the reconciliation in the release evidence.

## Files

- `docs/plans/multiplayer-v1/README.md`
- `docs/plans/hybrid-wolverine/README.md`
- `docs/plans/hybrid-wolverine/migration-checklist.md`
- `docs/plans/roadmap-v2/STATUS.md`
- `docs/plans/roadmap-v3/STATUS.md`
- `docs/plans/roadmap-v4/STATUS.md`
- `docs/plans/roadmap-v5/STATUS.md`
- new `Genelife.UnitTests/Docs/CommandCoverageTests.cs`

## Acceptance

- MP05 reflects the actual intent drain.
- H16/H19/H20 status matches L01.
- V2/V3/V4 status matches the code.
- Every command has an admission, drain, authorization and audit path.
- Every durable event has a projection or a documented exception.
- The docs/code consistency check passes.
- No status page claims a feature is done without evidence.

## Testing

- Run the command coverage test.
- Run the event projection coverage test.
- Run the docs consistency check.
- Manually review the changed status pages.
- Assert a deliberately stale reference fails the check.

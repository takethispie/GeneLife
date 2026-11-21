# Step 13 — Persistence and lifecycle

**Depends:** 06
**Goal:** Make the open world survive restarts and clean shutdowns.

## Context

`StartAsync` is a no-op and `Stop()` disposes the world; nothing persists.

## Tasks

- [ ] Choose a store (Mongo/Postgres/SQLite); document the decision.
- [ ] Define a versioned snapshot schema.
- [ ] Serialize/deserialize all persisted components using `NetworkId`.
- [ ] Auto-load on startup; create a new world only if no snapshot exists.
- [ ] Periodic snapshot save plus an append-only event log.
- [ ] Graceful shutdown: stop loop, save final snapshot, flush log.
- [ ] Add admin save/restore tooling.
- [ ] Handle old/corrupt schema explicitly.

## Files likely touched

- new `Genelife.Api/Persistence/*`
- `Genelife.Api/Services/SimulationManager.cs`, `Program.cs`
- `Genelife.Api/DTOs/*`

## Acceptance

- Restart resumes within one snapshot interval.
- Schema version is recorded and checked.
- Shutdown is clean and loss is bounded.

## Testing

- Kill/restart; assert world identity and recent changes.
- Corrupt/old schema handling.

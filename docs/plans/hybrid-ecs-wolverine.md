# ECS + Wolverine Hybrid Architecture

**Status:** Implemented — H01–H20 in place; H16/H19/H20 have remaining follow-ups tracked in the hybrid-wolverine checklist.

## Principle

Keep Arch ECS on the hot path. Add JasperFx Wolverine + Marten as the command, event, and persistence layer around it. The simulation engine stays lean and deterministic. Everything outside the tick loop — commands, reads, projections, sagas, client push — goes through Wolverine.

```
                  ┌─────────────────────────┐
 Player action ──▶│  Wolverine HTTP endpoint │
                  └───────────┬─────────────┘
                              │ Send(command)
                  ┌───────────▼─────────────┐
                  │  Command handler          │
                  │  - validate               │
                  │  - authorize              │
                  │  - enqueue to engine      │
                  └───────────┬─────────────┘
                              │
         ┌────────────────────┼────────────────────┐
         │                    ▼                    │
         │  ┌─────────────────────────────────┐    │
         │  │        SimulationEngine          │    │
         │  │  Tick(1/20s)                     │    │
         │  │  ┌──────────┐  ┌──────────────┐ │    │
         │  │  │NeedsDecay│  │  Decision    │ │    │
         │  │  └──────────┘  └──────────────┘ │    │
         │  │  ┌──────────┐  ┌──────────────┐ │    │
         │  │  │  Action  │  │   Hiring     │ │    │
         │  │  └──────────┘  └──────────────┘ │    │
         │  │  ┌──────────┐  ┌──────────────┐ │    │
         │  │  │ Payroll  │  │ JobSeeker    │ │    │
         │  │  └──────────┘  └──────────────┘ │    │
         │  │                                 │    │
         │  │  After tick: snapshot + events  │    │
         │  └─────────────┬───────────────────┘    │
         │                │ Publish(event)          │
         │                ▼                        │
         │  ┌─────────────────────────────────┐    │
         │  │     Event handlers               │    │
         │  │  - Marten projection updates     │    │
         │  │  - Saga transitions              │    │
         │  │  - SignalR client push           │    │
         │  └────────────┬────────────────────┘    │
         │               │                         │
         │               ▼                         │
         │  ┌─────────────────────────────────┐    │
         │  │     Read model (Marten docs)     │    │
         │  │  SimStatus, CompanyStatus        │    │
         │  └────────────┬────────────────────┘    │
         │               │                         │
         └───────────────┼─────────────────────────┘
                         │ Query
                         ▼
              ┌─────────────────────┐
              │  Read endpoints      │
              │  GET /sims           │
              │  GET /companies      │
              └─────────────────────┘
```

## Layer ownership

| Layer | Technology | Owns | Does NOT own |
|-------|-----------|------|-------------|
| **Simulation core** | Arch ECS | World, components, systems, tick loop, system scheduling | Commands, auth, persistence, client communication |
| **Command layer** | Wolverine | Validation, authorization, command→mutation translation, intent queue | Executing mutations (hands off to engine) |
| **Event layer** | Wolverine + Marten | Event emission from engine, projection updates, saga transitions | Generating events (engine owns that) |
| **Read model** | Marten | Document projection from events, query endpoints | Writing (engine + command layer own writes) |
| **Realtime** | Wolverine + SignalR | Hub messages triggered by events, client subscriptions | Simulation state (reads from Marten) |
| **Persistence** | Marten + Postgres | Save/load world state, event store, snapshot serialization | World format (engine owns the World representation) |

## Wave 4 adoption (commands, auth, realtime)

Wave 4 currently plans: MP09 ownership/command auth, MP10 SignalR transport, MP12 2D spatial, MP13 persistence. With Wolverine:

### MP09 — Command authorization
Replace custom auth middleware with Wolverine's `IMessageMiddleware`. Register `AuthorizationMiddleware` in Wolverine's options. Every command handler gets automatic player-id validation. Admin commands get role checks the same way.

**Current plan:** Custom `Commands/*` classes + manual auth in endpoints.
**Wolverine approach:** Wolverine HTTP endpoints map directly to handlers. `[WolverinePost("/api/sims/{simId}/eat")]` + `[Middleware(typeof(OwnershipMiddleware))]` on the handler class.

### MP10 — SignalR transport
Replace raw SignalR hub registration with Wolverine's SignalR integration. When the engine emits a `SimActionCompleted` event, a Wolverine event handler calls `IMessageBus.SendAsync(new SimSnapshotChanged(...))`, which cascades to SignalR via a Wolverine-side-effect `PublishAsync` to the hub group.

**Current plan:** Manual `IHubContext<SimHub>` injection in snapshot/event code.
**Wolverine approach:** Event handler → `PublishAsync(new SimStateUpdated(simId, snapshot))` → Wolverine routes to `SimHub` automatically.

### MP13 — Persistence
Replace custom snapshot serialization with Marten projections. When the engine emits `SimCreated`, `SimHungerChanged`, `ActionStarted`, Marten's inline projection updates `SimDocument`. On restart, load all documents, reconstruct the World by replaying or hydrating from the last snapshot.

**Current plan:** New `Persistence/*` classes, custom serialization.
**Wolverine approach:** Marten `IDocumentSession` in engine scoped setup. `WorldRestoreService` on startup reads all active documents and creates ECS entities.

## Wave 5 adoption (delta replication, hardening, scaling)

### MP11 — Delta snapshots
When the tick finishes, compare new WorldSnapshot to previous. Emit only changed entities as `EntityChanged` events instead of full snapshots. Wolverine batches these for efficient transport.

### MP14 — Acceptance testing
Replay recorded command sequences through the Wolverine test harness (`IHostBuilder().UseWolverine(...)`). Assert handler outputs and event sequences without a live tick loop.

### Perf07–09 — Tick hardening + CI guardrails
- Perf07: Wolverine's built-in metrics + OpenTelemetry exports replace custom `Metrics` class
- Perf08: Wolverine's handler registration replaces hand-built `ISimulationSystem` registry
- Perf09: `wolverine.AssertNoExceptionsWereLogged()` in CI tests replaces custom telemetry checks

### Perf10–12 — Data hygiene + scaling
- Perf10: Marten's document model forces clean component boundaries (no stale fields)
- Perf11: Wolverine's message batching reduces per-entity overhead in projections
- Perf12: Triggered scaling — add more Wolverine listener nodes when tick duration exceeds threshold

## Migration path (non-disruptive)

### Phase 1 — Add Wolverine alongside existing code (Wave 4)
1. Add `Wolverine` and `Marten` NuGet packages
2. Register Wolverine in `Program.cs` alongside existing services
3. Create command+event types as POCOs in `Genelife.Messages/`
4. Implement one command end-to-end: `POST /sims/{id}/eat` → `EatCommand` → handler enqueues → engine applies → emits `SimAteEvent` → Marten projection updates
5. Existing HTTP endpoints continue working. Wolverine endpoints run in parallel.

### Phase 2 — Replace read paths (Wave 4)
6. Switch `GetAllSims`/`GetAllCompanies` from snapshot to Marten query
7. Remove manual `WorldSnapshot` build from tick loop (keep for delta replication only)
8. Remove `ExecuteWithWorld` from read paths

### Phase 3 — Replace persistence (Wave 5)
9. Implement `WorldRestoreService` using Marten documents
10. Implement `WorldSaveService` that serializes World on shutdown
11. Add CI test: start simulation, add entities, stop, restart, verify world state matches

### Phase 4 — Replace auth + realtime (Wave 5)
12. Migrate JWT endpoints to Wolverine HTTP with middleware
13. Migrate SignalR hubs to Wolverine-side-effect-triggered events
14. Remove ASP.NET fallback endpoints once parity is confirmed

## Risks

| Risk | Mitigation |
|------|-----------|
| Marten projection latency could lag behind tick | Use inline projections for critical data, async for non-critical |
| Two code paths during migration causes confusion | Flags were used through Phase 2 and removed in V5 feature 05; either the old path or the new path exists, never both |
| Wolverine learning curve for team | Wolverine is convention-over-configuration. Start with one command handler to build familiarity |
| Postgres round-trip for every projection update is slower than in-memory snapshot | Batch projection updates. One Marten session per tick, one `SaveChangesAsync` at the end |
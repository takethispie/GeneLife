# H04 — First command flow: EatCommand end-to-end

**Phase:** 1 — Foundation
**Depends:** H02, H03
**Estimate:** 2 sessions

**Status:** Done

## Goal

Implement the first complete command pipeline: a player sends `POST /sims/{networkId}/eat`, it flows through Wolverine validation, reaches a handler that enqueues the mutation to the simulation engine, the engine applies it on the next tick, and a `SimActionStarted` event is emitted.

This proves the hybrid pattern works end-to-end.

## Context

The current architecture has no command queue. Player mutations (via `SimulationManager.AddSim`) call `ExecuteWithWorld` under the tick lock. With Wolverine, commands arrive asynchronously and are executed by the engine on the next tick.

## Tasks

- [x] Create `EatCommandHandler` in `Genelife.API/Handlers/`:
  - Accepts `SetSimAction` command
  - Validates the Sim exists via Marten query
  - Pushes the action into a thread-safe `CommandQueue` on the engine
  - Returns immediately (202 Accepted)
- [x] Add `CommandQueue` to `SimulationEngine`:
  - `ConcurrentQueue<EnqueuedCommand>` field
  - `Enqueue(ulong networkId, string actionType)` method (lock-free)
  - `DrainCommands()` called at start of each `Tick()`:
    - For each command, resolve entity via `NetworkIdRegistry`
    - Set `CurrentAction` component directly on the entity
    - Track entities modified this tick
- [x] After tick, for each modified entity:
  - Build a `SimActionStarted` event
  - Publish to Wolverine's `IMessageBus`
- [x] Add an event handler that logs `SimActionStarted` events
- [x] Write test:
  - Create Sim via `SimulationManager`
  - Send `SetSimAction(NetworkId, "Eat")` via Wolverine
  - Wait one tick
  - Assert Sim's `CurrentAction.Type == Eating`
  - Assert `SimActionStarted` event was published

## Files

- new `Genelife.API/Handlers/EatCommandHandler.cs`
- `Genelife.API/Services/SimulationEngine.cs` — added CommandQueue, DrainCommands, event publishing
- new `Genelife.API/Handlers/EventLogHandler.cs` — test-only log handler

## Key decisions

- **CommandQueue is not a Wolverine feature.** The engine owns it. Wolverine delivers the command to a handler. The handler hands it to the engine. The engine applies it on the tick. This keeps ECS deterministic.
- **Events are published after the tick, not during.** No structural changes during event publishing either.
- **NetworkId → Entity resolution is fragile** if entities are deleted between command enqueue and tick execution. Guard with `world.IsAlive(entity)`.

## Acceptance

- Full round-trip: HTTP → Wolverine → handler → CommandQueue → tick → entity mutated → event published → handler logs event
- No existing tests broken
- Engine still runs at 20 Hz with an empty command queue (zero overhead)

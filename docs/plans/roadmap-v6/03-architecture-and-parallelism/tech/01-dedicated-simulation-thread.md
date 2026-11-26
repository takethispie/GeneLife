# Dedicated Simulation Thread

## Context

`SimulationLoop` is a `BackgroundService`; each tick runs on a thread-pool thread shared with ASP.NET request handling. Request load can delay the timer callback. The command queue exists, but some request paths still take the world lock directly.

## Design

- Run the simulation loop on a dedicated long-running thread (`TaskCreationOptions.LongRunning` or a dedicated thread).
- Route all world mutations through the command queue; request threads enqueue typed commands and never mutate the world.
- Keep read paths on the published snapshot.

## Tasks

- [ ] Move the loop to a dedicated long-running thread.
- [ ] Audit request paths for direct world mutation and route them through the command queue.
- [ ] Confirm the timer callback is not delayed by request load.
- [ ] Run the 32-player soak and record tick jitter and dropped ticks.

## Files

- `Genelife.Application/Services/SimulationLoop.cs`
- `Genelife.Application/Services/SimulationEngine.cs`
- Command queue and request handlers in `Genelife.API`

## Contracts

- Command semantics and ordering are unchanged.
- No API contract changes.

## Acceptance

- The tick runs on a dedicated thread.
- Request load does not delay the timer callback.
- No request thread mutates the world outside the command queue.
- The 32-player soak shows no dropped ticks from thread-pool starvation.

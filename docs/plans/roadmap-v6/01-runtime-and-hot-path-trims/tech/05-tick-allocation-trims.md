# Tick Allocation Trims

## Context

`SimulationEngine.Tick` allocates `List<object> events = new()` every tick (`Genelife.API/Services/SimulationEngine.cs:520`). It also builds `TickCompleted` even when there is no `documentStore` or `MessageBus` consumer. At 20 ticks per second this is steady, avoidable allocation.

## Design

- Reuse a pooled event list across ticks, clearing it at tick start.
- Only build `TickCompleted` and enqueue events when a consumer is registered.
- Avoid LINQ and intermediate collections in the tick path.

## Tasks

- [ ] Replace the per-tick `new List<object>()` with a pooled or reused list.
- [ ] Gate event construction on the presence of a consumer.
- [ ] Measure allocation per tick in the 1k and 5k runs.
- [ ] Confirm event ordering and content are unchanged when a consumer exists.

## Files

- `Genelife.API/Services/SimulationEngine.cs`

## Contracts

- Event payloads and ordering are unchanged when a consumer exists.
- No event is emitted when no consumer exists (same as today's intent).

## Acceptance

- Per-tick allocation drops in the 1k and 5k runs.
- Event tests and replay tests pass.
- No change to event content or ordering.

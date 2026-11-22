# Step 05 — Command queue and intents

**Depends:** 04
**Goal:** Ensure the world is mutated only by the tick thread through validated intents.

**Status:** Partial — bounded queue, enqueue endpoints, and overflow policy are in place; full intent set, tick-time draining, coalescing, and range/cooldown validation are outstanding.

## Context

HTTP requests currently mutate the live world via `ExecuteWithWorld` under the tick lock.

## Tasks

- [ ] Define intent records: `MoveIntent`, `InteractIntent`, `UseObjectIntent`, `ChatIntent`, `AdminCommand`.
- [x] Add a bounded `CommandQueue` (`Channel<T>` or lock-backed ring buffer).
- [ ] Drain the queue at tick start; apply intents before running systems.
- [ ] Add a validation hook (ownership, range, cooldown, legality).
- [ ] Coalesce stale movement intents per player.
- [x] Convert mutation endpoints to enqueue + return an ack/error.
- [x] Define overflow policy (reject newest vs drop oldest).

## Files likely touched

- new `Genelife.Api/Commands/*`
- `Genelife.Api/Services/SimulationEngine.cs`, `Endpoints/*`

## Acceptance

- No request thread touches `World`.
- Queue is bounded; overflow behavior is explicit and logged.
- Intents are applied in a defined order at tick start.

## Testing

- Concurrent submit/drain test; overflow test; ordering test.

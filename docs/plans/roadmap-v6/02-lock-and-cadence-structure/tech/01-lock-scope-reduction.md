# Lock Scope Reduction and Double Buffering

## Context

`SimulationEngine.Tick` holds one global `lock (@lock)` for the entire tick (`Genelife.API/Services/SimulationEngine.cs:526`). The same lock is taken by `World`, `Economy`, `MapVersion`, `CurrentTick`, `GetPlayerGenosNetworkId`, `BuildMapStatus` and `ExecuteWithWorld`. Request and broadcast threads contend with the tick thread.

## Design

Split the tick into two phases:

1. **Mutate under lock.** Drain commands and run systems under the write lock.
2. **Publish outside lock.** Build an immutable snapshot and publish it with `Interlocked.Exchange`.

Move event collection and enqueueing outside the lock. Readers take the published snapshot or a read lock, never the write lock.

This is the foundation for the reader/writer accessor step and the async snapshot build in feature 03.

## Tasks

- [ ] Identify the minimal set of state that must be mutated under the lock.
- [ ] Move `CollectGenosEvents` and `CollectMapEvents` outside the lock.
- [ ] Publish the tick's world state via `Interlocked.Exchange`.
- [ ] Update accessors to read the published state.
- [ ] Run the 32-player soak and record lock wait time and dropped ticks.

## Files

- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.API/Services/SimulationLoop.cs`

## Contracts

- Command ordering and tick semantics are unchanged.
- Readers see a consistent world state, one tick at a time.

## Acceptance

- The tick thread does not hold the write lock while building snapshots or enqueueing events.
- The 32-player soak shows reduced lock contention and no dropped ticks from lock waits.
- Replay and determinism tests pass.

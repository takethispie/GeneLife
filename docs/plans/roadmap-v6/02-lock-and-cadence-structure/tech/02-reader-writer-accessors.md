# Reader/Writer Accessors

## Context

After the lock scope reduction, read-only accessors still take the same lock as writers. Read-only calls (`World`, `CurrentTick`, `MapVersion`, `GetPlayerGenosNetworkId`) do not need to serialize against each other.

## Design

Use `ReaderWriterLockSlim` for accessors that read world state, or read the published immutable snapshot for read-only paths.

- Writers take the write lock.
- Readers take the read lock or read the published snapshot.
- `BuildMapStatus` and `GetPlayerGenosNetworkId` become read-only paths.

## Tasks

- [ ] Replace the accessor lock with `ReaderWriterLockSlim` or a snapshot read.
- [ ] Convert `BuildMapStatus` and `GetPlayerGenosNetworkId` to read-only.
- [ ] Confirm no accessor mutates world state.
- [ ] Run the 32-player soak and record read latency.

## Files

- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.API/Services/SnapshotBroadcastService.cs`

## Contracts

- Accessor return values are unchanged.
- No API contract changes.

## Acceptance

- Read-only accessors do not serialize against each other.
- The 32-player soak shows no read latency regression and no new dropped ticks.
- Replay and determinism tests pass.

# Async Snapshot Build

## Context

`SimulationEngine.Tick` builds `BuildGenosSnapshots` and `BuildCompanySnapshots` inside the tick (`Genelife.Application/Services/SimulationEngine.cs:686-687`). Snapshot construction counts against the 50 ms tick budget. The tick only needs to publish the tick's world state; a consumer thread can build the snapshot and delta.

## Design

- The tick thread publishes the world state and returns.
- A dedicated snapshot consumer thread builds `WorldSnapshot` and `WorldSnapshotDelta`, feeds `SnapshotHistory` and the broadcast service.
- Readers read the latest published snapshot; the delta is built asynchronously.

## Tasks

- [ ] Add a snapshot consumer thread or channel.
- [ ] Move `BuildGenosSnapshots` and `BuildCompanySnapshots` off the tick thread.
- [ ] Feed `SnapshotHistory` and `SnapshotBroadcastService` from the consumer.
- [ ] Confirm readers see a consistent latest snapshot.
- [ ] Run the 2k and 32-player runs and record tick time without snapshot cost.

## Files

- `Genelife.Application/Services/SimulationEngine.cs`
- `Genelife.API/Services/SnapshotBroadcastService.cs`
- `Genelife.API/Snapshots/SnapshotHistory.cs`

## Contracts

- Snapshot and delta contents are unchanged.
- Readers see a consistent latest snapshot.

## Acceptance

- Snapshot construction no longer counts against the tick budget.
- The 2k and 32-player runs stay inside the tick budget with no dropped ticks.
- Snapshot equality and replay tests pass.

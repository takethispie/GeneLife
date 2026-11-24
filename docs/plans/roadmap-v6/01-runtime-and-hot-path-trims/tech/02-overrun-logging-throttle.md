# Overrun Logging Throttle

## Context

`SimulationLoop.LogOverrun` calls `engine.GetMetricsSnapshot()` on every overrun tick (`Genelife.API/Services/SimulationLoop.cs:102-116`). That call takes the metrics lock, sorts systems and builds strings. During a sustained overrun this becomes a self-inflicted cost: the act of reporting the overrun makes the next tick slower.

The dropped-tick path already throttles its warning to at most once every 5 s (`Genelife.API/Services/SimulationLoop.cs:81-98`). The overrun path does not.

## Design

Reuse the same throttle pattern as the dropped-tick warning:

- Store the last overrun log timestamp.
- Only build the metrics snapshot and log when at least 5 s have elapsed since the last overrun log.
- Always increment `OverrunCount`; only the expensive log is throttled.

## Tasks

- [ ] Add a `lastOverrunLogTimestamp` field to `SimulationLoop`.
- [ ] Gate the `GetMetricsSnapshot()` call and `LogWarning` behind the 5 s check.
- [ ] Keep `OverrunCount` incrementing on every overrun.
- [ ] Run the 1k `LoadHarness` under a forced overrun and confirm the log volume drops.

## Files

- `Genelife.API/Services/SimulationLoop.cs`

## Contracts

- `OverrunCount` and `DroppedTicks` keep their current meaning.
- The log message format stays the same.

## Acceptance

- A sustained overrun no longer produces one metrics snapshot per tick.
- The overrun log still appears at least once every 5 s during a sustained overrun.
- Counters remain accurate.

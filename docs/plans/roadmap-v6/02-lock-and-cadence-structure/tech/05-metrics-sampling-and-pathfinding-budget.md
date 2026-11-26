# Metrics Sampling and Pathfinding Budget

## Context

`SystemRegistry.Update` calls `GC.GetAllocatedBytesForCurrentThread()` and `Stopwatch.GetTimestamp()` twice per system per tick. `Metrics.RecordSystem` takes a shared lock per system per tick (`Genelife.Application/Services/Metrics.cs:63-79`). At 20 Hz with many systems this is measurable overhead.

`PathfindingSystem` runs every tick and calls `TilePathfinder.FindPath` with a `budget * 256` iteration limit (`Genelife.Application/Systems/PathfindingSystem.cs:44`). A single expensive path can blow the 50 ms budget.

## Design

- Sample system timings every N ticks instead of every tick.
- Make `RecordSystem` lock-free with per-system counters.
- Lower the per-tick pathfinding budget or increase its cadence; measure the effect on movement quality.

## Tasks

- [ ] Add a sampling interval for per-system timing.
- [ ] Replace the shared metrics lock with per-system counters.
- [ ] Tune the pathfinding budget and cadence against the 2k run.
- [ ] Record p95/p99 and dropped ticks before and after.

## Files

- `Genelife.Application/Services/Metrics.cs`
- `Genelife.Application/Systems/SystemRegistry.cs`
- `Genelife.Application/Systems/PathfindingSystem.cs`

## Contracts

- Metrics remain accurate enough for the overrun log and guardrails.
- Movement behavior stays within the accepted quality bar.

## Acceptance

- Metrics overhead is bounded and no longer takes a shared lock per system per tick.
- Pathfinding stays inside the tick budget.
- The 2k run shows no p95 regression and no dropped ticks from pathfinding.

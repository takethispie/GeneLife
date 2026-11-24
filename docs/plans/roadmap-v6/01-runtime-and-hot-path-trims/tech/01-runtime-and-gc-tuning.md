# Runtime and GC Tuning

## Context

`Genelife.API.csproj` has no GC, PGO or globalization settings. The API runs with workstation GC defaults. With high per-tick allocation from snapshots, events and lists, workstation GC pauses can push ticks past the 50 ms budget.

## Design

Add build settings to `Genelife.API.csproj`:

```xml
<ServerGarbageCollection>true</ServerGarbageCollection>
<ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>
<TieredPGO>true</TieredPGO>
<InvariantGlobalization>true</InvariantGlobalization>
<PublishReadyToRun>true</PublishReadyToRun>
```

- Server GC reduces pause frequency under allocation pressure.
- Concurrent GC keeps gen2 collection off the tick thread where possible.
- Tiered PGO improves steady-state throughput.
- Invariant globalization removes ICU startup and lookup cost.
- ReadyToRun improves startup for release builds.

These are build-time settings. No code change.

## Tasks

- [ ] Add the settings to `Genelife.API.csproj`.
- [ ] Confirm `dotnet build GeneLife.sln` is green.
- [ ] Run the 1k and 5k `LoadHarness` fixtures and record `Gen2CollectionsPerMinute` and p95/p99.
- [ ] Confirm no determinism test changes.

## Files

- `Genelife.API/Genelife.API.csproj`

## Contracts

- No API contract changes.
- No behavior change.

## Acceptance

- The build is green with the new settings.
- The 1k and 5k runs show no p95 regression and a lower or equal gen2 collection rate.
- Replay and determinism tests pass.

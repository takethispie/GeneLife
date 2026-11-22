# R07 — Observability and performance

**Phase:** 5 — Observability
**Depends:** R02, R04, M10
**Estimate:** 2 sessions
**Status:** Planned (V2)

## Goal

Measure map and replication cost so V2 can prove that the static map and AOI design stay within the existing bandwidth and tick budgets.

## Context

V1 already measures replication bytes per client, snapshot build time, system timing/allocation and scaling recommendations. `ScalingTriggers` recommends `InterestManagement` when bytes per client exceed 64 KB/s and `SpatialGrid` when decision work dominates.

V2 adds a map channel, map deltas and viewport-filtered sim deltas. These need explicit counters; otherwise a large map snapshot or an expensive viewport group is invisible until a client complains.

## Design

### Metrics

```csharp
public sealed record MapReplicationMetricsSnapshot(
    int MapVersion,
    int MapSnapshotBytes,
    long MapSnapshotsSent,
    long MapDeltasSent,
    long MapFullFallbacks,
    double MapBytesPerClientPerSecond,
    int ClientsWithViewport,
    int ClientsWithoutViewport,
    double AverageVisibleSims,
    double AverageDeltaBytes,
    int ClientsOverBudget);
```

Record:

- map snapshot size and count;
- map delta size/count;
- full-map fallback count and reason;
- per-client map bytes per second;
- viewport coverage;
- filtered vs unfiltered delta bytes;
- building version lag (client version vs live version);
- placement failure counts from M10.

### Performance guardrails

Extend the existing performance suite with:

- 1k/2k/5k NPC fixtures;
- 32 connected clients;
- map snapshot under a configured byte ceiling;
- filtered delta under a per-client byte ceiling;
- map version change broadcast cost;
- no per-client full-world scan;
- tick p95 and allocation ceilings.

Budgets should be configuration, recorded in a baseline file like V1, not hard-coded into assertions.

### Health and alerts

Add map/replication health checks:

- live map version behind the last emitted event;
- client map version gap;
- map snapshot above budget;
- AOI disabled while bytes/client exceed threshold;
- full-map fallback rate above threshold.

Expose through `/api/simulation/metrics`, `/api/map/health` and the admin map page.

### Scaling trigger integration

- `InterestManagement` is recommended by existing triggers; R04 wiring makes it actionable.
- If map snapshots dominate bandwidth, recommend a viewport-filtered map subscription (future).
- If pathfinding dominates, the movement budget from P10 is the first response, not parallelism.
- Do not auto-enable scaling actions; record the recommendation.

## Tasks

- [ ] Add map/replication counters to `Metrics` and `ReplicationMetrics`.
- [ ] Add map snapshot/delta size and fallback counters.
- [ ] Add viewport coverage and over-budget counters.
- [ ] Add map/replication health checks.
- [ ] Extend the metrics endpoint and admin map page.
- [ ] Add 1k/2k/5k guardrails with 32 clients.
- [ ] Add payload-size baselines.
- [ ] Add `ScalingTriggers` coverage for map/replication signals.

## Files

- `Genelife.API/Services/Metrics.cs`
- `Genelife.API/Services/ReplicationMetrics.cs`
- `Genelife.API/Services/ScalingTriggers.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.API/Services/SnapshotBroadcastService.cs`
- `Genelife.UnitTests/Performance/PerformanceGuardrailTests.cs`
- `Genelife.UnitTests/Snapshots/ReplicationMetricsTests.cs`

## Acceptance

- Map snapshot and delta sizes are measured per connection.
- Full-map fallbacks are counted with a reason.
- Viewport coverage and over-budget clients are visible.
- 32-client guardrails pass within the configured budgets.
- The scaling evaluator can recommend interest management from real metrics.
- Health checks distinguish warning from critical conditions.
- No per-client query scans the full world.

## Testing

- Counter tests.
- Payload-size regression.
- Full-fallback reason test.
- 32-client guardrail run.
- Scaling trigger test.
- Health-check threshold test.

# P10 — Movement metrics and guardrails

**Phase:** 7 — Observability
**Depends:** P02, P05, P09
**Estimate:** 1–2 sessions
**Status:** Planned (V2)

## Goal

Measure player intent throughput, rejection reasons, movement cost and pathfinding cost so V2 can prove it is safe at the target scale.

## Context

V1 has `Metrics`, `ReplicationMetrics`, `ScalingTriggers` and performance guardrails at 1k/2k/5k NPC tiers. V2 adds player intents and pathfinding, both of which can become hot paths if implemented carelessly.

The existing `ScalingTriggers` can already recommend `SpatialGrid`, `InterestManagement`, `ParallelPureSystems` and `LoadShedding`. Movement and pathfinding metrics should feed those decisions.

## Design

### Movement metrics

```csharp
public sealed record MovementMetricsSnapshot(
    long IntentsAdmitted,
    long IntentsRejected,
    IReadOnlyDictionary<string, long> RejectionsByCode,
    int ActiveMovers,
    int PendingPathRequests,
    long PathRequests,
    long PathFailures,
    long PathBudgetExceeded,
    double AveragePathLength,
    double PathfindingMilliseconds,
    double MovementMilliseconds,
    int PlayersBehindAck);
```

Record per tick:

- admitted/rejected intents;
- active movers;
- tiles moved;
- path requests, failures, budget exhaustion;
- pathfinding/movement system time and allocation;
- players whose acknowledged tick lags the current tick beyond a threshold.

### Metrics and endpoint

- Add movement counters to `MetricsSnapshot`.
- Expose them through `GET /api/simulation/metrics`.
- Include the scaling decision with the existing `ScalingTriggers` payload.
- Add map-page alerts for high rejection rates or pathfinding budget pressure.

### Guardrails

Extend `PerformanceGuardrailTests` with:

- 1k, 2k and 5k NPC fixtures;
- 32 simulated player intents per tick (or a realistic subset);
- no pathfinding for abstract NPC commuting;
- per-tick allocation ceiling;
- p95 tick budget;
- map snapshot/delta size ceiling.

The V2 default should keep NPC commuting on entry teleports. If a `CommuteMode.AnimateVisible` experiment is enabled, it must have its own budget and be off in the default guardrail.

### Scaling triggers

- If pathfinding or movement dominates tick time, recommend load shedding or a smaller path budget before parallelizing.
- If replication bytes grow with player viewports, recommend `InterestManagement`.
- If decision/pathfinding cost dominates at high NPC counts, the existing `SpatialGrid` recommendation applies.
- Do not enable any scaling action automatically in this roadmap; record the recommendation as V1 does.

## Tasks

- [ ] Add movement/pathfinding counters to `Metrics`.
- [ ] Add rejection counters by code.
- [ ] Add system time/allocation for `MovementSystem` and `PathfindingSystem`.
- [ ] Expose the snapshot through the metrics endpoint and admin map page.
- [ ] Add 1k/2k/5k guardrails with player intents.
- [ ] Add a no-NPC-pathfinding-by-default assertion.
- [ ] Add a replication-size budget for map + movement deltas.
- [ ] Add metrics tests and a performance report.

## Files

- `Genelife.API/Services/Metrics.cs`
- `Genelife.API/Services/ScalingTriggers.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- new `Genelife/Map/MovementMetrics.cs`
- `Genelife.UnitTests/Performance/PerformanceGuardrailTests.cs`
- `Genelife.UnitTests/Performance/MultiplayerAcceptanceTests.cs`

## Acceptance

- Metrics report admitted/rejected intents and rejection reasons.
- Movement and pathfinding appear in per-system timing/allocation.
- The metrics endpoint exposes the movement snapshot.
- 1k/2k/5k guardrails pass with player intents.
- Abstract NPC commuting performs no per-tick pathfinding.
- A pathfinding budget overrun is recorded, not hidden.
- Scaling recommendations are recorded when thresholds are crossed.

## Testing

- Metrics counter tests.
- Rejection-code aggregation.
- Budget-overrun metric.
- Guardrail runs.
- Metrics endpoint contract.
- No-pathfinding assertion.

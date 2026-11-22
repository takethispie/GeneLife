# L21 — Metrics and balance

**Phase:** 10 — Observability
**Depends:** L01–L20
**Estimate:** 2 sessions
**Status:** Implemented (V1)

## Goal

Track whether the lifecycle produces a stable, plausible society over decades and catch demographic collapse early.

## Context

The lifecycle is slow: problems take many simulated years to appear. Yearly metrics and health checks are the only practical way to tune it.

## Design

`PopulationMetricsSystem` order 620, cadence 1,200, writes `PopulationSnapshot`:

```csharp
public sealed record PopulationSnapshot(
    long Tick,
    int Year,
    int Total,
    int Education,
    int Work,
    int Retirement,
    int Elderly,
    int StudentsEnrolled,
    int GraduatesThisYear,
    int Employed,
    int Unemployed,
    int RetiredThisYear,
    int DeathsThisYear,
    int ArrivalsThisYear,
    decimal AverageAge,
    decimal MedianAge,
    decimal AverageHealth,
    decimal AverageAgeAtDeath,
    decimal LifeExpectancy,
    decimal PensionReplacementRate,
    decimal PensionFundingRatio,
    decimal WorkingAgeShare,
    decimal OldAgeDependencyRatio,
    IReadOnlyList<int> PyramidBuckets,
    IReadOnlyList<(string Cause, int Count)> DeathCauses);
```

### Health checks

| Check | Warn | Fail |
|---|---|---|
| Working-age share | < 50% | < 40% |
| Old-age dependency ratio | > 0.45 | > 0.70 |
| Enrollment rate (6–17) | < 90% | < 75% |
| Graduate employment after 2 years | < 70% | < 50% |
| Pension funding ratio | < 0.8 | < 0.5 |
| Average health (65+) | < 50 | < 35 |
| Population change per year | < −3% | < −8% |
| Life expectancy | < 65 | < 55 |

Alerts are structured and emitted once per failing year, not per sim.

### Balance targets

- Education 15–20%;
- Work 55–60%;
- Retirement 15–20%;
- Elderly 3–7%;
- life expectancy 70–85;
- replacement rate 30–60%;
- dependency ratio 0.25–0.45.

## Tasks

- [ ] Add `PopulationSnapshot` and the yearly aggregator.
- [ ] Compute pyramid, life expectancy and dependency ratio.
- [ ] Persist snapshots and add history endpoints.
- [ ] Add health checks and admin alerts.
- [ ] Extend the lifecycle page with historical charts.
- [ ] Add a 10-year guardrail test against the targets.
- [ ] Keep aggregation O(sims) once per year.

## Files

- new `Genelife.API/Services/PopulationMetrics.cs`
- new `Genelife/Systems/PopulationMetricsSystem.cs`
- new `Genelife.API/Models/PopulationSnapshotDocument.cs`
- `Genelife.API/DTOs/LifecycleStatus.cs`
- `Genelife.Web/Pages/Lifecycle.razor`

## Acceptance

- One snapshot per simulated year with all fields.
- Pyramid buckets sum to the total population.
- Life expectancy and dependency ratio match a reference computation.
- Health endpoint flags a collapsed scenario.
- Admin can compare years and see failing indicators.
- No per-tick aggregation.

## Testing

- Aggregation fixture test.
- Pyramid sum test.
- Life-expectancy reference test.
- Health threshold table test.
- 10-year guardrail scenario.

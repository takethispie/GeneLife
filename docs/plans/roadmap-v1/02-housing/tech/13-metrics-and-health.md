# H13 — Metrics and health

**Phase:** 10 — Observability
**Depends:** H05–H10, E16
**Estimate:** 1–2 sessions
**Status:** Planned

## Goal

Expose housing conditions that predict collapse: vacancy, rent burden, arrears, evictions, overcrowding and homelessness.

## Context

The economy exposes money metrics. Housing failure is slower and easier to miss: vacancy drifts, rents creep, arrears age and suddenly a wave of evictions appears. Metrics must catch the drift.

## Design

`HousingSnapshot` captured yearly by `HousingMetricsSystem` order 611, cadence 1,200, and merged into the admin read model:

```csharp
public sealed record HousingSnapshot(
    long Tick,
    int Year,
    int Properties,
    int Units,
    int OccupiedUnits,
    int VacantUnits,
    int ShelterBeds,
    int ShelteredHouseholds,
    int HomelessHouseholds,
    int OvercrowdedUnits,
    int UnitsBelowCondition,
    decimal VacancyRate,
    decimal RentIndex,
    Money RentRoll,
    Money RentCollected,
    Money RentArrears,
    Money UtilityArrears,
    Money UpkeepPaid,
    int EvictionsThisYear,
    int ConstructionUnits,
    decimal MedianRentBurden,
    decimal ShelterOccupancy);
```

Computation notes:

- Median rent burden over renter households with income > 0.
- Arrears ageing buckets: 1, 2, 3–5, 6+ months.
- Condition buckets: < 0.2, 0.2–0.5, > 0.5.
- Evictions counted from events, not inferred from state.

Health checks:

| Check | Warn | Fail |
|---|---|---|
| Vacancy rate | < 2% or > 10% | < 1% |
| Homeless households | > 3% | > 7% |
| Shelter occupancy | > 90% | > 100% (overflow) |
| Rent burden median | > 40% | > 55% |
| Rent arrears / rent roll | > 10% | > 25% |
| Overcrowded units | > 8% | > 15% |
| Units below condition 0.2 | > 5% | > 12% |

Storage: ring buffer plus Marten `HousingSnapshotDocument`; endpoints:

- `GET /api/housing/metrics/current`
- `GET /api/housing/metrics/history`
- `GET /api/housing/health`

## Tasks

- [ ] Add `HousingSnapshot` and aggregation from the registry.
- [ ] Add event counters for evictions, moves and construction.
- [ ] Implement health checks and structured warnings.
- [ ] Persist snapshots off the tick thread.
- [ ] Add endpoints and admin charts.
- [ ] Add a 10-year scenario assertion against the bands.
- [ ] Keep aggregation O(units) once per year, not per tick.

## Files

- new `Genelife.API/Services/HousingMetrics.cs`
- new `Genelife/Systems/HousingMetricsSystem.cs`
- new `Genelife.API/Models/HousingSnapshotDocument.cs`
- `Genelife.API/Endpoints/Wolverine/HousingEndpoints.cs`
- `Genelife.Web/Pages/Housing.razor`

## Acceptance

- One housing snapshot per simulated year.
- Arrears buckets and condition buckets sum to their totals.
- Health endpoint flags a deliberately broken scenario.
- Admin can compare housing over multiple years.
- No per-tick scan of the housing stock.

## Testing

- Aggregation fixture test.
- Bucket sum test.
- Health threshold table test.
- 10-year guardrail scenario.

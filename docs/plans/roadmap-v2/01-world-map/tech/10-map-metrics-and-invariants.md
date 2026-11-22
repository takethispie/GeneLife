# M10 — Map metrics and invariants

**Phase:** 6 — Observability
**Depends:** M03, M08
**Estimate:** 1–2 sessions
**Status:** Planned (V2)

## Goal

Make map consistency observable: counts, density, placement failures and reconciliation issues should be visible before they become a corrupt save or a client-side mystery.

## Context

V1 has `EconomyMetricsSystem`, `HousingMetricsSystem` and `PopulationMetricsSystem` recording yearly snapshots, plus `HousingHealthCheckStatus` in the read model. The map needs the same treatment.

The expensive invariant checks must not run every tick. They run on restore, on admin placement and in tests; the yearly metrics system records cheap aggregate counts.

## Design

### Metrics system

`MapMetricsSystem`:

- order `612` (after housing metrics at `611`);
- cadence `1200` (one in-game year);
- queries `Placement` once, iterates the occupancy grid once;
- records into an economy-style history list.

```csharp
public sealed record MapSnapshot
{
    public long Tick { get; init; }
    public int Year { get; init; }
    public int Buildings { get; init; }
    public int Residential { get; init; }
    public int Offices { get; init; }
    public int Schools { get; init; }
    public int OccupiedTiles { get; init; }
    public int ReservedTiles { get; init; }
    public int FreeWalkableTiles { get; init; }
    public int UnplacedBuildings { get; init; }
    public int PlacementFailures { get; init; }
    public decimal BuiltDensity { get; init; }
    public int OverlapIssues { get; init; }
}
```

### Health checks

Add `MapHealthCheckStatus` entries to `WorldDataStatus` / `MapStatus`:

- unplaced buildings > 0;
- overlap issues > 0;
- occupied tiles outside map bounds;
- map version mismatch;
- building count mismatch between components and `WorldMap`;
- lot exhaustion in a zone used by construction.

Levels follow the existing housing health-check pattern: `Ok`, `Warning`, `Critical`.

### Reconciliation

`MapReconciler.Verify(World world, WorldMap map)` returns structured issues:

```csharp
public readonly record struct PlacementIssue(
    ulong? NetworkId,
    string Code,
    string Message,
    TileCoord? Tile);
```

It checks:

1. every building entity has exactly one `Placement`;
2. every placement is inside bounds and has a valid footprint/entry;
3. no two footprints overlap;
4. no placement exists without a live building entity;
5. every building has a network id;
6. `WorldMap` occupancy equals recomputed occupancy.

Restore refuses to continue on `Critical` issues. Admin placement runs a targeted version of the check after applying.

### Allocation and cadence

- Metrics run once per 1,200 ticks; allocation is allowed to be proportional to building count but should reuse buffers.
- Reconciliation is not registered as a per-tick system.
- `WorldMap` queries used by metrics allocate only for the result list.
- The read model exposes the last snapshot; no per-request world scan.

## Tasks

- [ ] Add `MapSnapshot` and `MapMetricsSystem` at order 612.
- [ ] Add map counters to the metrics snapshot and yearly history.
- [ ] Add `MapHealthCheckStatus` and map health endpoint data.
- [ ] Implement `MapReconciler.Verify`.
- [ ] Run reconciliation on restore and after admin placement.
- [ ] Add `PlacementFailure` counters by error code.
- [ ] Add metrics/health tests.
- [ ] Add a 1,200-tick metrics cadence test.

## Files

- new `Genelife/Systems/MapMetricsSystem.cs`
- new `Genelife/Map/MapReconciler.cs`, `MapSnapshot.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.API/DTOs/MapStatus.cs`, `WorldDataStatus.cs`
- `Genelife.API/Services/Metrics.cs`
- `Genelife.UnitTests/Map/MapMetricsTests.cs`, `MapReconcilerTests.cs`

## Acceptance

- Map metrics run once per simulated year and produce a stable snapshot.
- Health checks report unplaced, overlapping and out-of-bounds buildings.
- Restore fails on critical reconciliation issues.
- Admin placement failure increments a counter by error code.
- Metrics and health are visible through `/api/map` and `/api/simulation/metrics`.
- No per-tick map scan is added.

## Testing

- Metrics cadence test.
- Counts after placement/removal.
- Overlap detection.
- Unplaced building detection.
- Map version mismatch health check.
- Metrics allocation smoke test.

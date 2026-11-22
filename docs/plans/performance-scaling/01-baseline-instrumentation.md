# Step 01 — Baseline instrumentation

**Depends:** none
**Goal:** Establish reproducible measurements before changing anything.

**Status:** Done

## Tasks

- [x] Per-system stopwatch; log the top consumers periodically.
- [x] Tick duration histogram (p50/p95/p99) and effective tick rate.
- [x] Allocations per tick.
- [x] Gen0/1/2 collection counts per minute.
- [x] Entity count and churn (created/destroyed per minute).
- [x] Command queue depth and per-tick drained count.
- [x] Snapshot build time and bytes per client.
- [x] Read-API latency while the tick runs.

## Files likely touched

- `Genelife.Api/Services/SimulationEngine.cs`
- new `Genelife.Api/Services/Metrics.cs`

## Acceptance

- Metrics are emitted for 1k/5k/10k NPC fixtures.
- Numbers are recorded in this plan as the baseline.

## Testing

- Verify counters change correctly under a known workload.

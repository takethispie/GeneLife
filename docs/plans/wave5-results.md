# Wave 5 Results

**Branch:** `feature/wave-5` (based on the Wave 4 commit)
**Status:** implemented and verified locally. Changes are intentionally left uncommitted for the
coordinator to review and commit once.

## How to run checks

```bash
dotnet build Genelife.UnitTests/Genelife.UnitTests.csproj --nologo
dotnet run --project Genelife.UnitTests/Genelife.UnitTests.csproj --no-build --no-launch-profile
```

Result on the implementation machine:

```
Total: 249, Errors: 0, Failed: 0, Skipped: 2, Time: ~7s
```

The two skips are the pre-existing manual 10k harness fixtures. Test parallelization is now
disabled at assembly level (`Genelife.UnitTests/TestAssembly.cs`): the same suite reproduced an
`AccessViolationException` on the baseline commit when Arch worlds were exercised in parallel, so
the suite now runs deterministically.

## W5-A — MP11 delta snapshots and reconnect

| Item | Where |
|---|---|
| Ring buffer of recent snapshots (default 128 ticks) | `Genelife.API/Snapshots/SnapshotHistory.cs` |
| Per-client deltas, removed ids, full fallback | `Genelife.API/Snapshots/WorldSnapshotDelta.cs` |
| Last acknowledged tick per connection | `Genelife.API/Snapshots/ReplicationTracker.cs` |
| `sinceTick` request + acknowledgement | `WorldHub.RequestSnapshot`, `WorldHub.AcknowledgeSnapshot` |
| Broadcast prefers deltas per client | `SnapshotBroadcastService.cs` |
| Position + tick for interpolation | `SimulationEngine.BuildSimSnapshots`, `SimStatus.X/Y` |
| Bytes per client per second | `ReplicationMetrics` |

Wire contract:

- `SendSnapshot` carries a full `WorldSnapshot`.
- `SendSnapshotDelta` carries a `WorldSnapshotDelta` (`full=false`, `sinceTick` set, removed ids).
- `RequestSnapshot(sinceTick?)` resumes from the supplied tick or the connection's last
  acknowledgement. Unknown/expired ticks produce a full snapshot.
- `AcknowledgeSnapshot(tick)` enables per-connection delta broadcasts.
- Needs are quantized to 0.1 and positions/timers to 0.01 in snapshots so deltas only carry
  meaningful changes instead of every floating-point decay step. A test asserts a one-entity
  change produces a smaller payload than the full snapshot.

## W5-B — MP14 acceptance and load testing

Acceptance harness: `Genelife.UnitTests/Performance/MultiplayerAcceptanceTests.cs`.

- 32 authenticated SignalR clients connect to one world, receive consistent snapshots containing
  32 distinct sims, and submit move intents successfully.
- Reconnect after a gap inside the history window resumes with `SendSnapshotDelta` whose
  `sinceTick` equals the acknowledged tick. Acknowledgements are tracked per player, so a new
  connection resumes without the client passing `sinceTick`; a separate test covers that path.
- Save → stop → restore preserves exact network ids, names, and positions.
- Unknown/expired ticks return a full snapshot (covered by `SnapshotHistoryTests`).

### Recorded tick/alloc numbers (Apple Silicon dev machine, 25 warm-up + 200 measured ticks)

The harness now drives `SimulationEngine.Tick` (systems + snapshot construction + history) so the
numbers include the replication path added in this wave.

| Fixture | p50 ms | p95 ms | alloc/tick | GC0 |
|---|---:|---:|---:|---:|
| 1k synthetic | 0.16 | 0.20 | 90,786 B | 0 |
| 1k realistic | 0.16 | 0.20 | 90,787 B | 0 |
| 5k synthetic | 0.54 | 2.82 | 503,247 B | 0 |
| 5k realistic | 0.55 | 2.88 | 503,274 B | 0 |

Budgets in `PerformanceThresholds`: 1k 10 ms / 150 KB, 5k 25 ms / 800 KB, 10k 50 ms / 1.6 MB per
tick. The 1k baseline (`performance-baselines/1k-synthetic-baseline.json`) records 0.5 ms / 95 KB
with a 3x tolerance. Bandwidth is measured per connection by `ReplicationMetrics`; the acceptance
test exercises both full and delta payloads.

## W5-C — Perf07 harden tick loop

- `FixedStepScheduler` (new) owns fixed-step accumulation, bounded catch-up, and backlog drop.
- `SimulationLoop` reports `OverrunCount`, `DroppedTicks`, `LastTickMilliseconds`, budget, target
  rate, and effective rate. Overruns log the slowest systems with per-system allocation.
- `GET /api/simulation/metrics` (admin) exposes loop counters, `MetricsSnapshot`, replication
  bytes, and the scaling decision.
- Tests: `FixedStepSchedulerTests`.

## W5-D — Perf08 registry/cadence + Perf09 guardrails

- `ISimulationSystem`, `SimulationContext`, `Cadence`, and `SystemRegistry` define explicit order
  and cadence. Duplicate order/name registration throws.
- Systems now expose order/cadence and no longer own hidden interval counters; the registry is the
  single scheduling point. Payroll tests drive the registry.
- `Metrics.RecordSystem` now records per-system allocated bytes; `SystemMetric` exposes totals and
  averages, and metric log lines include per-system allocation.
- CI: `.github/workflows/performance.yml` runs the harness + guardrails + acceptance tests, uploads
  JSON reports, and writes a run summary. Budgets: `PerformanceThresholds`; stored baseline:
  `performance-baselines/1k-synthetic-baseline.json` (compared with a 3x tolerance). Guardrails now
  run both the 1k and 5k tiers.

## W5-E — Perf10 data hygiene + Perf11 pass reduction

- `JobPosting.Requirements` and `JobSeeker.Skills` are now `Skill` bitmask flags (no `List<T>`).
  Hiring matches with `BitOperations.PopCount`. `ComponentHygieneTests` enforces the absence of
  list fields.
- Snapshot construction is a single pass per archetype (was two passes plus an O(N²) name match).
- `SimulationEngine.TryGetEntity` uses the `NetworkIdRegistry` map first and only falls back to a
  scan for entities created outside the registry.
- The engine owns the registry used by repositories and persistence, so authorization lookups are
  map-backed in the normal path.

## W5-F — Perf12 triggered scaling

No expensive scaling work was activated because no recorded trigger was met. `ScalingTriggers`
evaluates the actual `MetricsSnapshot` + `ReplicationMetricsSnapshot` and returns the action that
*would* be justified: spatial grid when AI/decision passes dominate, AOI when bytes/client exceed
64 KB/s, parallelism when pure systems dominate, and load shedding when p95 exceeds 3x budget.
The spatial index and AOI filter already exist from Wave 4 and remain dormant behind that gate.
Tests: `ScalingTriggerTests`.

## Review follow-ups applied

- `RequestSnapshot` is rate limited (5 requests/second per connection) and rejects via
  `SnapshotRejected`.
- Acknowledgements are tracked per player and copied to a new connection, so reconnects resume
  without an explicit `sinceTick`; acknowledgements before the first published tick are rejected.
- `SnapshotHistory.BuildDelta` constructs deltas outside the history lock, and the broadcast groups
  connections by acknowledged tick, builds one delta per group, and estimates payload bytes once
  per group.
- Snapshot build duration uses total milliseconds; action names are cached to avoid per-sim string
  allocation.
- The harness drives `SimulationEngine.Tick` with 25 warm-up + 200 measured ticks, and a 5k
  guardrail tier was added.
- Tests assert the changed sim in a delta and exact names/ids/positions across restore.
- Removed dead API (`SnapshotHistory.Latest`/`Capacity`, `FixedStepScheduler.Step`/`MaxCatchUp`),
  the unreachable `NeighborScan` branch, and the duplicated engine scheduling loop (the registry
  now owns timed execution).

## Open items

- Command intents are still queued but not drained by the engine; this is inherited from Wave 4 and
  outside Wave 5's scope. The acceptance test validates transport, auth, and queue admission.
- Replication byte counts are measured by serializing the outgoing payload with the same JSON
  contract; this is accurate but adds serialization cost when the broadcast service runs. It can be
  disabled if it ever shows up in profiles.

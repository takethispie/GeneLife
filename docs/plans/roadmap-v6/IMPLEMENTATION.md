# Roadmap V6 — Implementation Record

**Status:** Implemented. This file records what landed, the measurements taken, and the two
changes that are deliberately opt-in.

**Scope guard:** no gameplay, economy, housing, family or social behavior changed on the
default path. Replay, balance, family-release, multiplayer and performance suites pass.

## What landed

### Tier 1 — Runtime and hot-path trims

| Step | Change | Files |
|---|---|---|
| 1.1 | Server GC, concurrent GC, tiered PGO, R2R and invariant globalization for the API | `Genelife.API/Genelife.API.csproj` |
| 1.2 | Overrun logging throttled to once per 5 s; `OverrunCount` still increments on every overrun | `Genelife.API/Services/SimulationLoop.cs` |
| 1.3 | Company snapshots build one office-count dictionary per snapshot instead of an Office query per company | `Genelife.API/Services/SimulationEngine.cs` |
| 1.4 | Appearance descriptor cache keyed by `(traits, age band, version, stable key)`; cleared on `Initialize`/`Shutdown`, so restore rebuilds it | `Genelife.API/Services/SimulationEngine.cs` |
| 1.5 | Per-tick event list is pooled; sim/map event collection and `TickCompleted` are skipped when no persistence/event consumer is attached | `Genelife.API/Services/SimulationEngine.cs` |

### Tier 2 — Lock and cadence structure

| Step | Change | Files |
|---|---|---|
| 2.1 | `Tick` mutates under the write lock, then releases it and collects events / publishes snapshots under the read lock. Snapshot publication is still `Interlocked.Exchange` + `SnapshotHistory.Record`. | `Genelife.API/Services/SimulationEngine.cs` |
| 2.2 | Global `lock (@lock)` replaced by `ReaderWriterLockSlim`. Read-only accessors (`World`, `Economy`, `MapVersion`, `CurrentTick`, `EmploymentIndex`, `Relationships`, `GetPlayerSimNetworkId`, `GetPlayerTile`, `TryGetEntity`, `IsAlive`, `GetOwnerPlayerId`, `GetMetricsSnapshot`, `PrepareSave`) take the read lock; mutating paths take the write lock. | `Genelife.API/Services/SimulationEngine.cs` |
| 2.3 | `Cadence` gained an `Offset`; staggered systems run later but receive the logical boundary tick. Implemented as an opt-in (`SimulationEngine.StaggerCadence`, default off — see below). | `Genelife/Systems/ISimulationSystem.cs`, `SystemRegistry.cs`, family/payroll systems, `SimulationEngine` |
| 2.4 | `DecisionSystem`, `NeedsDecaySystem` and `ActionSystem` use `TryGet` instead of `Has` + `Get` double lookups. Merging the passes was rejected: `DecisionSystem` sits between decay and action and must observe post-decay needs. | `Genelife/Systems/*` |
| 2.5 | Per-system timing is sampled every 5 ticks (periodic systems are always timed); `Metrics.RecordSystem` is lock-free (interlocked microsecond counters); pathfinding has a per-tick iteration cap (`MovementOptions.MaxPathIterationsPerTick = 16_384`) charged against actual expansions. | `Genelife/Systems/SystemRegistry.cs`, `Genelife.API/Services/Metrics.cs`, `Genelife/Systems/PathfindingSystem.cs`, `Genelife/Map/TilePathfinder.cs`, `Genelife/Map/MovementOptions.cs` |

### Tier 3 — Architecture and parallelism

| Step | Change | Files |
|---|---|---|
| 3.1 | The fixed-step loop runs on a dedicated long-running thread instead of a thread-pool continuation. The legacy admin mutations (`AddSim`, `AddCompany`, `DeleteSim`, `SetSimAction`) now enqueue typed admin commands and wait for the tick to apply them, so request threads no longer mutate the world. | `Genelife.API/Services/SimulationLoop.cs`, `SimulationManager.cs`, `SimulationEngine.cs`, `Commands/AdminCommand.cs`, `Genelife.Domain.Tests/Services/AdminCommandQueueTests.cs` |
| 3.2 | Per-entity parallel passes for needs decay, decision and action using Arch `InlineParallelEntityQuery`, behind `SimulationEngine.ParallelSystems` (default off — see below). A determinism test compares 600 sims serial vs parallel. | `Genelife/Systems/{NeedsDecay,Decision,Action}System.cs`, `SimulationParallelism.cs`, `Genelife.Domain.Tests/Systems/ParallelSystemDeterminismTests.cs` |
| 3.3 | Snapshot construction moved to a dedicated consumer thread with a coalescing signal. Enabled by the hosted `SimulationLoop` (`AsyncSnapshots = true`); direct `Tick` callers (tests, harness) keep synchronous snapshots. | `Genelife.API/Services/SimulationEngine.cs`, `SimulationLoop.cs`, `Genelife.Domain.Tests/Snapshots/AsyncSnapshotTests.cs` |

## Opt-in decisions (with evidence)

The roadmap assumed cadence staggering and parallel per-entity passes would be free wins. The
measurements say otherwise on this codebase, so both are implemented but default off. The
roadmap's own rule is: keep a change only if it moves p95/p99/allocation/dropped ticks without
breaking determinism or the V5 acceptance runs.

- **Cadence staggering (`StaggerCadence`).** Offsets 0/2/4/6/8/10/12 for the monthly family
  chain and 3/6/9 for the pay-cycle systems make monthly systems fire on different ticks.
  However, the decade balance run is sensitive to the order in which coupled family/payroll
  passes see the world. With offsets enabled, `balance-200-21` failed its median rent burden
  band (0.4369 vs 0.40; Gini 0.7866 vs 0.70). With offsets zeroed the same seed passes
  (0.3765 rent burden, 0.595 Gini). Even logical boundary ticks did not recover the bands
  (0.4073 rent burden, 0.776 Gini), because every-tick systems still interleave differently.
  Per-interval run counts and same-seed determinism are unchanged, but the V5 balance bands
  are part of acceptance, so the default keeps offsets at zero.
- **Parallel per-entity passes (`ParallelSystems`).** The passes are deterministic across
  chunks (proved by `ParallelSystemDeterminismTests`), but chunk scheduling costs more than
  the work for these cheap per-entity loops on the 1k/5k harness in Debug builds. Enabling it
  raised 5k p95 from ~6.4 ms to ~11 ms. It remains available for release-build experiments
  and much larger worlds.

## Deviations from the plan

- **Merged entity passes (02-04).** `DecisionSystem` sits between `NeedsDecaySystem` and
  `ActionSystem` in the order, and `WorkScheduleSystem`, `AgingSystem`, `EducationSystem` and
  friends run between them. Merging would make decision see pre-decay needs or action see
  post-work-schedule state, changing outcomes. The retained change is the `TryGet` dedupe of
  `Has` + `Get`; full-world iteration count is unchanged.
- **Command-queue audit (03-01).** The legacy admin mutations now go through typed admin
  commands and `ExecuteAdminCommandSync`, which blocks the request thread until the next tick
  applies the command (2 s timeout). The only remaining direct mutation is `RestoreSnapshot`,
  which is explicitly an offline operation and refuses to run while the simulation is running.
- **Harness fixture coverage.** `SimFixture.PopulateSynthetic/Realistic` creates sims without
  `NetworkId` or `Traits`, and no companies/offices. That means `CollectSimEvents` matches
  nothing, appearance derivation is skipped, and the company snapshot index has nothing to
  index, so the Tier 1 event/appearance/company wins do not show up in the harness allocation
  numbers even though they remove real work in production. Adding those components to the
  fixture would make the harness measure the production path; it was left unchanged here to
  keep the historical reports comparable.

## Measurements

Protocol: `LoadHarness` 1k/5k synthetic and realistic fixtures, 225 ticks, Debug build, single
run each. The machine is a shared dev laptop, so p95/p99 vary between repeats: after the
changes, 1k p95 was observed between 0.20 ms and 3.0 ms, and 5k p95 between 5.9 ms and
15.5 ms. Allocation is stable and repeatable.

| Fixture | Baseline p95 | After p95 | Baseline alloc/tick | After alloc/tick |
|---|---:|---:|---:|---:|
| 1k synthetic | 2.74 ms | 0.20 ms | 261,719 B | 262,170 B |
| 1k realistic | 0.23 ms | 0.20 ms | 262,208 B | 262,170 B |
| 5k synthetic | 6.43 ms | 6.08 ms | 1,351,964 B | 1,351,892 B |
| 5k realistic | 6.43 ms | 5.95 ms | 1,351,930 B | 1,351,892 B |

The largest repeatable win is the removal of per-system timing instrumentation on 4 of every 5
ticks (metrics sampling) plus skipping the full-world event collection when no consumer is
attached. Allocation is dominated by the economy systems (`BasicIncome`, `ConsumerSpending`,
`UtilityConsumption`), not by snapshot construction or event lists, so the Tier 1 allocation
trims do not move the harness allocation numbers much.

The 5k fixture exceeds the 2,000-sim target by 2.5x and stays far below the 50 ms p95/p99
budget. `MultiplayerAcceptanceTests` (32-player soak) passes.

## Verification commands

```bash
dotnet build GeneLife.sln
dotnet run --project Genelife.Domain.Tests/Genelife.Domain.Tests.csproj -- -trait- "Category=Scenario"
dotnet run --project Genelife.Performance.Tests/Genelife.Performance.Tests.csproj -- \
  -class Genelife.UnitTests.Performance.LoadHarness \
  -class Genelife.UnitTests.Performance.PerformanceGuardrailTests \
  -class Genelife.UnitTests.Performance.FamilyPerformanceTests \
  -class Genelife.UnitTests.Performance.MultiplayerAcceptanceTests \
  -class Genelife.UnitTests.Economy.BalanceScenarioTests \
  -class Genelife.UnitTests.Family.ReplayTests \
  -class Genelife.UnitTests.Release.FamilyReleaseScenarioTests
```

## Known follow-ups

- Re-measure `ParallelSystems` and `StaggerCadence` in a Release build and on a quiet machine
  before deciding whether either should become the default.
- The legacy admin endpoints now wait up to 50 ms for the next tick; if that latency matters,
  convert them to the `202 Accepted` + `/api/commands/{id}` pattern used by the newer admin
  endpoints.

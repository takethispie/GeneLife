# V6 opt-in dossier — cadence staggering and parallel per-entity passes

**Author:** @roberto (Solution Researcher) · **Date:** 2026-11-26 · **Branch:** `research/roberto/genelife-research`
**Verified against:** `master` @ `4bba63a` (worktree `G:/dev/GeneLife-roberto`)
**Card:** "Research dossier: Arch ECS parallel-query/scheduler pitfalls + cadence-staggering prior art"

## 0. Status of the question this dossier was asked to inform

The dossier was commissioned to de-risk main-dev's V6 opt-in re-measurement, described as the last open
release gate. **That gate has since closed, so this is a decision record plus a re-entry checklist, not
an open research question.** Both V6 opt-ins are already resolved in the tree:

| Opt-in | State | Evidence |
|---|---|---|
| `SimulationEngine.StaggerCadence` (cadence phase offsets) | Implemented, **default off**, stays off | Release re-measurement merged in `cf36bda`; decision text in `docs/plans/roadmap-v6/IMPLEMENTATION.md` (Opt-in decisions); board card notes "no Release gain (2k gate p95 1.97 ms off vs 2.72 ms on, within noise)" |
| `SimulationEngine.ParallelSystems` (Arch `InlineParallelEntityQuery` passes) | **Removed entirely** — passes are serial-only | `65df943` *"fix(sim): remove parallel passes that leaked foreground threads"*; `docs/plans/roadmap-v6/IMPLEMENTATION.md` step 3.2 |

**Do not conflate two different band misses.** They are distinct causes and the distinction is easy to
get wrong in a report:

- **Staggering band shift** (measured only in the staggered arm, same seed): `balance-200-21` median rent
  burden 0.4369 vs the 0.40 ceiling, Gini 0.7866 vs 0.70; offsets zeroed, same seed, pass at 0.3765 /
  0.595. This is an artefact of the opt-in and is the reason the default stays zero.
- **Live default-path miss**: median rent burden 0.4041 at 200/seed 21 (per `IMPLEMENTATION.md` known
  follow-ups), 0.4041 / 0.4619 / 0.4619 and Gini 0.7802 / 0.7699 / 0.7881 at 200/1k/2k (`a87d745`).
  pedro bisected this to `5ddc300` (monthly consumption pass Order 460 → 506), **not** to staggering.
  `cf76f3d` corrected the V5 release record: 1k rent burden is 0.4619 (not the recorded 0.4284) and 1k
  Gini 0.7699 was over the ceiling and never flagged.

## 1. What bites when Arch parallel passes are enabled

### P1 — Foreground worker threads that outlive the world (the hard blocker)

- `Arch.Core.World.SharedJobScheduler` is a **static** property (`Arch` 2.1.0, `src/Arch/Core/World.cs:92`).
- `World.Dispose()` never touches it: `Core/World.cs:516-533` disposes world state only — the scheduler is
  process-wide by design ("Worlds can share `JobScheduler`, so in theory only one is required",
  `Arch.Docs` → `documentation/optimizations/multithreading.md`).
- The scheduler Arch dispatches to is the **ZeroAllocJobScheduler** package (assembly `Schedulers.dll`).
  In the version we actually resolve — `zeroallocjobscheduler/1.1.2`, pulled transitively by Arch — the
  worker threads are created as **foreground** threads: a byte scan of the shipped
  `~/.nuget/packages/zeroallocjobscheduler/1.1.2/lib/netstandard2.1/Schedulers.dll` contains
  `ThreadPrefixName`, `Notifier`, `WorkerLoop`, `MainThreadID`, `StrictAllocationMode` but **no
  `IsBackground` member reference** — i.e. nothing sets `IsBackground = true`.
- Consequence, reproduced in this repo: with `Environment.ProcessorCount == 8` the scheduler owns **7
  foreground threads**, created lazily by `ParallelQuery`/`InlineParallelEntityQuery`
  (`Arch` `Core/Jobs/World.Jobs.cs:94-140` → `SharedJobScheduler.Schedule`). xUnit v3 waits
  `ShutdownForegroundThreadWaitSeconds` (10 s) at end of run, then logs
  `[FATAL ERROR] Foreground threads were left running, forcing process exit` and calls
  `Environment.Exit(1)` — a green suite that still exits 1. Same latent effect in the API host: the
  threads are created on the simulation thread and never disposed, so the host cannot exit cleanly.
- Full local reproduction (symptom, `dotnet-stack` capture of the 7 blocked workers, root-cause chain,
  and the isolated trigger) is in the findings write-up `docs/findings/2026-11-25-foreground-threads-left-running.md`
  as committed in `65df943`. **Note:** that file was later deleted from the tree by `220d884`; retrieve it
  with `git show 65df943:docs/findings/2026-11-25-foreground-threads-left-running.md`.

Why this is a lifecycle bug and not a tuning knob: .NET keeps a process alive until all **foreground**
threads finish; background threads are abandoned at exit ([`Thread.IsBackground`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.thread.isbackground)).
A library that starts foreground workers it is never asked to stop owns your process lifetime.

### P2 — Disposal in 1.1.2 is thread-affine, and upstream's fix is not in the graph

- The 1.1.2 assembly contains a `MainThreadID` reference, matching the documented behaviour that
  `JobScheduler.Dispose()` refuses to run on any thread other than the one that constructed it. Host/DI
  shutdown and xUnit teardown both run on other threads, so a naive "dispose on shutdown" hook throws
  instead of fixing anything, and a `Lazy<JobScheduler>` cannot be reset to a healthy instance.
- Upstream has since fixed both: ZeroAllocJobScheduler **2.0.0** creates workers with
  `IsBackground = true` and the source comment *"Workers must not keep the process alive when the
  scheduler is not disposed"* (`JobScheduler/Worker.cs` on the default branch), and `Dispose()` no longer
  carries the thread restriction (`JobScheduler/JobScheduler.cs`, default branch).
- **But the Arch line has not adopted it.** NuGet metadata: `Arch` 2.1.0 (ours) *and* the latest
  `Arch` 2.2.0 both declare `<dependency id="ZeroAllocJobScheduler" version="1.1.2" />`. **Upgrading Arch
  does not remove the trap.**
- Actionable form: if this path is ever revived, either own the scheduler and dispose it on the creating
  thread, or pin the transitive dependency to `ZeroAllocJobScheduler` ≥ 2.0.0. The members Arch calls —
  `JobScheduler.Schedule(IJob)`, `Flush`, `CombineDependencies`, `JobHandle`, `IJob` — all still exist in
  the 2.0.0 source, so a pin is plausible; I have **not** built or run the swap (see §4).

### P3 — Parallel queries block the tick, and only pay when work per chunk is real

- `Arch.Docs` (`optimizations/multithreading.md`): *"Each of these calls, whether `world.ParallelQuery` or
  `world.InlineParallelQuery`, will **block** the main thread until the Query has been processed."*
  Parallelism therefore does not overlap the rest of the tick and every pass pays scheduling cost inside
  the tick budget.
- The same page warns: *"NOT thread-safe! Do not call a parallel query from anything but the main thread"*
  and *"you have to synchronise access to other objects yourself"* — the callback is only safe when it
  touches nothing shared outside the iterated components.
- Partitioning is by **chunk range per archetype**: `InlineParallelChunkQuery` builds
  `new RangePartitioner(Environment.ProcessorCount, archetypeSize)` and schedules one job per range
  (`Core/Jobs/World.Jobs.cs`). Cheap per-entity loops (needs decay, decision, action) therefore spend more
  in chunk scheduling and flushing than in the work — which is exactly what the measurement showed:
  enabling the passes raised 5k p95 from ~6.4 ms to ~11 ms in Debug (`IMPLEMENTATION.md` step 3.2).
- Hard rule inherited from our own design doc (`03-architecture-and-parallelism/tech/02-parallel-independent-systems.md`):
  *"Do not parallelize anything that creates, adds, removes or destroys entities, or that shares write
  access to the same component."*

### P4 — Determinism is not free, and "deterministic across chunks" is weak evidence

Chunk-range parallelism is reproducible *within* a pass only if each chunk's work is independent. Anything
that reads or writes shared state — derived indices (`EmploymentIndex`, `HousingRegistry`,
`LifecycleIndex`), pooled event lists, shared counters, or the RNG streams — makes the result depend on
completion order. GeneLife's determinism contract (bans `Random.Shared`, uses named `SimulationRandom`
streams, requires stable iteration order) is a *serial* contract; parallel passes put every one of those
guarantees back on the table. The retained guard is
`Genelife.Domain.Tests/Systems/EveryTickSystemsTests.cs` — 600 genoses × 20 ticks, asserting identical
Hunger/Energy/Hygiene/Bladder/Action/TimeRemaining across two runs of the serial passes.

## 2. Why cadence staggering moves balance bands (prior art)

Staggering is a **scheduling** change: `Cadence.ShouldRun` becomes `(tick - Offset) % Interval == 0`, so
the monthly family chain (offsets 0/2/4/6/8/10/12) and the pay-cycle systems (3/6/9) stop firing on the
same tick, and coupled passes observe each other in a different relative order.

**Bevy states the underlying principle exactly.** By default Bevy runs systems in parallel and their
relative order is not deterministic; when two systems conflict (one writes what the other reads/writes)
and no explicit ordering exists, the observable result depends on that non-deterministic order — Bevy
calls this a **system execution order ambiguity** and ships a detector for it, because the fix is an
explicit ordering, not hope ([`bevy::ecs::system`](https://docs.rs/bevy/latest/bevy/ecs/system/index.html),
[ambiguity-detection example](https://docs.rs/bevy/0.15.3/src/nondeterministic_system_order/nondeterministic_system_order.rs.html),
[Bevy 0.5 release notes](https://bevy.org/news/bevy-0-5/): *"When two systems interact with the same data,
but have no explicit ordering defined, the output they produce is non-deterministic"*).

GeneLife's monthly family/payroll chain is exactly such a coupled set — `RelationshipExposure`,
`Conception`, `Birth`, `FamilyHousehold`, `StateCare`, `Trust`, `Pregnancy` plus the pay-cycle systems all
read and write household/relationship/finance state. Spreading their execution ticks **is** an
order-ambiguity change, and the balance bands are the observable difference: 0.4369 / 0.7866 staggered vs
0.3765 / 0.595 with offsets zeroed, same seed.

Our own experiment already isolates the mechanism. Giving staggered systems the **logical boundary tick**
(`SystemRegistry.cs:70-72`, `new SimulationContext(context.Tick - Offset, ...)`) keeps tick-derived work
(RNG streams, due dates, event timestamps) identical — yet the bands still did not recover
(0.4073 rent burden, 0.776 Gini). So the sensitivity is not only at the month boundary; it is the ordering
*within* the tick of coupled ever-tick and periodic passes. Any future staggering must be validated
against the balance bands and the V5 acceptance runs, not just against run counts and determinism.

**The performance case for staggering is also weak on this codebase**, which is what settling the
re-measurement showed: 2k gate p95 **1.97 ms** with offsets zeroed vs **2.72 ms** enabled (worst 2.24 vs
3.05), 5k synthetic 10.83 → 9.87 ms (opposite direction), 1k realistic flat at 0.31 vs 0.28 ms, allocation
identical to within ~50 B/tick, and repeat-to-repeat spread at 5k (2-3 ms) wider than every delta. Against a
50 ms tick budget, there is no periodic spike to earn back — *my inference from those numbers, not a
separate measurement*.

## 3. Verdicts

1. **Cadence staggering — keep off (default `StaggerCadence = false`).** Settled by `cf36bda`: no Release
   gain, and a known balance-band shift. Revisit only if a real dropped-tick/overrun source appears at
   ≥2k genoses; if it does, keep the logical-boundary-tick trick, re-run the balance bands and the V5
   acceptance runs, and treat the ordered couple (family ↔ payroll ↔ consumption) as ordered, not
   interchangeable.
2. **Parallel per-entity passes — do not re-add on Arch's shared scheduler while the resolved
   `ZeroAllocJobScheduler` is 1.1.2.** Removing them (`65df943`) was the right call: the measured gain was
   negative and the lifecycle cost was a process that cannot exit.
3. **Re-entry checklist, if a future scale target demands parallelism** (in order):
   1. Fix the thread ownership first: own the `JobScheduler` lifecycle (dispose on the creating thread) or
      pin `ZeroAllocJobScheduler` ≥ 2.0.0; verify the swap with a process-exit test, not just a green suite.
   2. Prove independence per system: disjoint read/write sets, no structural changes
      (`Create`/`Add`/`Remove`/`Destroy`), no shared index/event/RNG writes inside the callback.
   3. Prove determinism on the replay suite and `EveryTickSystemsTests`, for NPC-only and 32-player worlds.
   4. Only then measure in **Release** on a quiet machine with the real harness.
   5. Fix the harness fixture first if the number matters: `GenosFixture.PopulateSynthetic/Realistic`
      creates genoses without `NetworkId`/`Traits` and no companies, so event, appearance and company work
      is invisible to it (`IMPLEMENTATION.md`, deviations).
4. **Pattern to copy for any in-house thread:** our own snapshot consumer already does it correctly —
   `new Thread(...) { IsBackground = true, Name = "genelife-snapshot-consumer" }`, with cancellation and a
   join on stop (`SimulationEngine.EnsureSnapshotConsumer` / `StopSnapshotConsumer`). The bug was never
   "threads are bad"; it was delegating thread ownership to a static third-party scheduler.

## 4. Open questions (not resolved here)

- **Is ZeroAllocJobScheduler 2.0.0 a drop-in for the members Arch 2.1.0 calls?** Member presence verified
  by reading 2.0.0 source; no build/run swap test was performed (out of scope for a dossier, and it would
  need a spike with a process-exit assertion).
- **Would a coarser partition make cheap per-entity loops profitable?** Arch partitions per archetype chunk
  range using `Environment.ProcessorCount`; with one archetype and ~2k entities the work per job is small.
  Not measured here.
- **Is the monthly spike real at all?** The Release re-measurement says the differences are within noise;
  no independent profile of per-tick overruns by tick-in-month was produced.

## Sources

**In-repo (verified at `4bba63a`)**
- `docs/plans/roadmap-v6/IMPLEMENTATION.md` — opt-in decisions, measurements, deviations, known follow-ups
- `docs/plans/roadmap-v6/03-architecture-and-parallelism/{summary.md,tech/02-parallel-independent-systems.md}`
- `docs/plans/roadmap-v6/02-lock-and-cadence-structure/tech/03-cadence-phase-staggering.md`
- `Genelife.Application/Systems/ISimulationSystem.cs` (`Cadence.ShouldRun`, `Offset`), `SystemRegistry.cs:70-72`
- `Genelife.Application/Services/SimulationEngine.cs` (`StaggerCadence` :223, `EnsureSnapshotConsumer` :705-719)
- `Genelife.Domain.Tests/Systems/EveryTickSystemsTests.cs`
- Commits: `cf36bda` (re-measurement merge), `65df943` (parallel passes removed + findings doc), `220d884`
  (findings deleted), `5ddc300` (default-path band break), `a87d745` / `f8b7ff5` / `cf76f3d` (band facts)

**Upstream / primary**
- Arch 2.1.0 source (`github.com/genaray/Arch`, tag `v2.1.0`): `Core/World.cs:92,516-533`,
  `Core/Jobs/World.Jobs.cs:94-140`, `Templates/World.InlineParallelEntityQuery.cs`
- Arch.Docs `documentation/optimizations/multithreading.md` (setup, blocking, thread-safety warnings)
- ZeroAllocJobScheduler (`github.com/genaray/ZeroAllocJobScheduler`): default-branch `JobScheduler/Worker.cs`
  (`IsBackground = true`, with the "must not keep the process alive" comment), `JobScheduler/JobScheduler.cs` (`Dispose`)
- Shipped `Schedulers.dll` from `~/.nuget/packages/zeroallocjobscheduler/1.1.2` (byte scan of member names)
- NuGet metadata: `arch/2.1.0/arch.nuspec` and `arch/2.2.0/arch.nuspec` → `ZeroAllocJobScheduler 1.1.2`
- [Thread.IsBackground (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/api/system.threading.thread.isbackground)
- [bevy::ecs::system — System ordering](https://docs.rs/bevy/latest/bevy/ecs/system/index.html),
  [ambiguity-detection example](https://docs.rs/bevy/0.15.3/src/nondeterministic_system_order/nondeterministic_system_order.rs.html),
  [Bevy 0.5 — New Parallel System Executor](https://bevy.org/news/bevy-0-5/)

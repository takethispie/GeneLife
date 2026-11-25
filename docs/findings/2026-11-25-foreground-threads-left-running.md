# Foreground threads left running in the domain suite

Date: 2026-11-25
Status: Fixed (option A: parallel passes removed)

## Symptom

The full domain suite passes all tests and then fails teardown:

```
=== TEST EXECUTION SUMMARY ===
   Genelife.Domain.Tests  Total: 578, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 7,886s
Waiting 10 seconds for foreground threads to exit...
[FATAL ERROR] Foreground threads were left running, forcing process exit
EXIT=1
```

The same fatal occurs when only `ParallelSystemDeterminismTests` runs:

```
Total: 1, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 0,091s
Waiting 10 seconds for foreground threads to exit...
[FATAL ERROR] Foreground threads were left running, forcing process exit
EXIT=1
```

A control run of a test class that never enters the parallel path exits 0:

```
Genelife.UnitTests.Systems.DecisionSystemTests  Total: 7 ... Failed: 0
EXIT=0
```

## Reproduction

Build and run the repo-native test executable (the current .NET 10 SDK rejects the
VSTest `dotnet test` path for this MTP project with a separate opt-in error):

```sh
dotnet build Genelife.Domain.Tests/Genelife.Domain.Tests.csproj -c Release
./Genelife.Domain.Tests/bin/Release/net10.0/Genelife.Domain.Tests -noColor
```

Isolated trigger:

```sh
./Genelife.Domain.Tests/bin/Release/net10.0/Genelife.Domain.Tests \
  -class Genelife.UnitTests.Systems.ParallelSystemDeterminismTests -noColor
```

## Root cause

`SimulationParallelism` installs a process-wide `Schedulers.JobScheduler` the first
time a parallel per-entity pass runs, and never disposes or stops it:

- `Genelife.Domain/Systems/SimulationParallelism.cs:13` creates the scheduler with
  `ThreadCount = Math.Max(1, Environment.ProcessorCount - 1)` and
  `ThreadPrefixName = "genelife-parallel"`.
- `SimulationParallelism.cs:20` assigns it to `World.SharedJobScheduler`.
- `SimulationParallelism.cs:24` exposes only `EnsureScheduler()`; there is no
  shutdown/reset path. The `Lazy<JobScheduler>` keeps it alive for the process.
- The three call sites are `NeedsDecaySystem.cs:37`, `DecisionSystem.cs:43` and
  `ActionSystem.cs:38`, all reached only when `parallel == true` and
  `world.Size >= 512`.

`Schedulers` 1.1.2 creates its worker threads with
`new Thread(ParameterizedThreadStart)` and starts them without setting
`IsBackground = true`. `JobScheduler.Config` has only `ThreadCount`,
`ThreadPrefixName`, `MaxExpectedConcurrentJobs` and `StrictAllocationMode`; there is
no background-thread option. On this machine `Environment.ProcessorCount == 8`, so
the scheduler owns 7 foreground threads.

xUnit v3's in-process runner (`xunit.v3` 4.0.3) waits
`ShutdownForegroundThreadWaitSeconds` (default 10) for foreground threads at the end
of the run, then logs `[FATAL ERROR] Foreground threads were left running, forcing
process exit` and calls `Environment.Exit(1)`.

The only test that constructs systems with `parallel: true` is
`Genelife.Domain.Tests/Systems/ParallelSystemDeterminismTests.cs:23`:

```csharp
var parallel = Run(parallel: true);
```

`Run` creates 600 genoses (`GenosCount`) and wraps the world in `using var world`.
The first parallel tick crosses the 512 threshold, so `EnsureScheduler()` runs and
the scheduler is created on the xUnit worker thread executing the test. Disposing
the Arch `World` does not dispose the static scheduler; Arch 2.1.0's `World.Dispose`
does not touch `World.SharedJobScheduler`.

A `dotnet-stack` capture taken during the 10-second shutdown window shows the seven
foreground workers blocked in the scheduler:

```
Thread (0x43454B):
  System.Private.CoreLib!System.Threading.WaitHandle.WaitMultiple(...)
  Schedulers!Schedulers.JobScheduler+Notifier.Wait()
  Schedulers!Schedulers.JobScheduler.WaitForTask(class Schedulers.Job&, class WorkerData)
  Schedulers!Schedulers.JobScheduler.WorkerLoop(class System.Object)
  ... (7 threads total)
```

## Why this is pre-existing and unrelated to recent changes

- The fatal is emitted after the test summary, during xUnit's process-shutdown
  check. It is not a test assertion failure or a product-logic failure.
- The scheduler is created only by the parallel determinism test; no change under
  test is needed to trigger it.
- The same fatal reproduces from a clean Release build of the current tree and from
  the isolated single test.

## Impact

- The suite exits 1 even when every test passes, so any acceptance or CI command
  that trusts the exit code fails.
- Production latent bug: if `SimulationEngine.ParallelSystems` is enabled
  (`Genelife.Application/Services/SimulationEngine.cs:230`, default `false`), the
  same 7 foreground threads exist in the API process. They are created on the
  simulation loop thread (`Genelife.Application/Services/SimulationLoop.cs:50-57`,
  `RunLoop` at line 61) and are never disposed, so the host cannot exit cleanly
  after shutdown.

## Why a naive dispose hook is not enough

`Schedulers.JobScheduler.Dispose()` checks `IsMainThread`
(`Thread.CurrentThread.ManagedThreadId == MainThreadID`, captured in the scheduler
constructor) and throws `InvalidOperationException("Can only call Dispose from the
thread that spawned the JobScheduler!")` otherwise. Host/DI shutdown and xUnit
teardown run on different threads from the tick thread, so disposing there would
throw. A correct disposal must run on the thread that first entered the parallel
path. The current `Lazy<JobScheduler>` also cannot be reset, so a disposed scheduler
would leave `World.SharedJobScheduler` pointing at a dead instance.

## Fix plan

### Recommended (A): Remove the parallel pass dependency on Arch's scheduler

The roadmap already measured this path as a loss and left it default-off
(`docs/plans/roadmap-v6/IMPLEMENTATION.md`: enabling `ParallelSystems` raised 5k
p95 from ~6.4 ms to ~11 ms). Arch 2.1.0's supported parallel entry points all
funnel through `World.SharedJobScheduler`; replacing it with thread-pool
parallelism would mean reimplementing the chunk partition over the public
`World.Archetypes`/`Chunk` surface, a larger rewrite for a path that is not
currently a win.

Steps:

1. Delete `Genelife.Domain/Systems/SimulationParallelism.cs`.
2. Remove the `parallel` field/constructor parameter and the
   `SimulationParallelism.EnsureScheduler()` calls from `NeedsDecaySystem`,
   `DecisionSystem` and `ActionSystem`; keep the serial queries.
3. Remove `SimulationEngine.ParallelSystems` and the parallel arguments at
   `SimulationEngine.cs:440,448,449`.
4. Delete or rewrite `ParallelSystemDeterminismTests`; if determinism coverage is
   still wanted, keep a serial-only regression.
5. Validate the full suite exits 0 and a shutdown thread dump contains no
   `Schedulers.JobScheduler.WorkerLoop` threads.

Trade-off: removes the opt-in experiment. Re-add it only together with a scheduler
that uses background threads.

### Alternative (B): Keep the feature and make scheduler threads background

1. Fork `ZeroAllocJobScheduler` 1.1.2 (Apache-2.0) or upstream a PR that sets
   `IsBackground = true` on the worker threads created in `JobScheduler`'s
   constructor (or adds a `UseBackgroundThreads` config flag).
2. Reference the patched package from `Genelife.Domain`.
3. Replace the `Lazy<JobScheduler>` in `SimulationParallelism` with a resettable
   lock + nullable field, and add `Shutdown()` that disposes the scheduler and
   nulls `World.SharedJobScheduler`.
4. Call `Shutdown()` from `SimulationLoop.RunLoop`'s `finally` (same thread) for
   deterministic cleanup; background threads also make process exit safe if a
   caller forgets to dispose.
5. Keep the determinism test; it can dispose its own scheduler or rely on the
   background-thread behavior.

Trade-off: a forked/internal package to maintain until upstream accepts the change.

### Stopgap (C): Thread-affine lifecycle without forking

- Production: add `SimulationParallelism.Shutdown()` and call it from
  `SimulationLoop.RunLoop`'s `finally`, on the simulation thread that created the
  scheduler; reset the static.
- Tests: expose an internal hook (or inject the scheduler) so the determinism test
  can create and dispose its scheduler on the same thread in a `finally`.
- Fragile: every future tick caller that enables parallel must own and dispose the
  scheduler. Use only if A/B cannot land.

### Not fixes

- `shutdownForegroundThreadWaitSeconds` only changes the wait; the run still fails.
- `Environment.Exit(0)`/`ProcessExit` handlers hide the leak and can mask other
  failures.
- Reflection cannot set `IsBackground` on the workers: `WorkerData` does not retain
  the `Thread` objects (confirmed by decompiling `Schedulers.dll` 1.1.2).

## Validation

```sh
dotnet build Genelife.Domain.Tests/Genelife.Domain.Tests.csproj -c Release
./Genelife.Domain.Tests/bin/Release/net10.0/Genelife.Domain.Tests -noColor
```

Expected: `Total: 578 ... Failed: 0`, exit 0, and no
`Waiting 10 seconds for foreground threads to exit...` line.

Also run the isolated determinism class and, for option B/C, verify with
`dotnet-stack` during shutdown that no `Schedulers.JobScheduler.WorkerLoop` threads
remain. For production, start the API with `ParallelSystems = true`, send SIGTERM,
and confirm the process exits.

## Resolution (option A)

Implemented on 2026-11-25:

- Deleted `Genelife.Domain/Systems/SimulationParallelism.cs` and the
  `Schedulers`-based scheduler entirely; nothing in the product assigns
  `World.SharedJobScheduler`.
- Removed the `parallel` constructor parameter, threshold branch and job structs
  from `NeedsDecaySystem`, `DecisionSystem` and `ActionSystem`; they run serial
  queries only.
- Removed `SimulationEngine.ParallelSystems` and the three parallel arguments.
- Replaced `ParallelSystemDeterminismTests` with `EveryTickSystemsTests`, a
  600-genos, 20-tick serial regression that runs the three passes twice and
  compares the resulting component values.
- Updated `docs/plans/roadmap-v6/IMPLEMENTATION.md` (step 3.2, opt-in decision and
  follow-up) to record the removal.

Result: `Total: 578, Errors: 0, Failed: 0`, exit code 0, and no
`Waiting 10 seconds for foreground threads to exit...` line. The feature can be
re-added only together with a scheduler that starts background threads and can be
disposed from the tick thread.

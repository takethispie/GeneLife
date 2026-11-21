# Wave Execution Handoff

**Branch:** `refactor/arch`
**Wave 1 status:** committed in `a48e3ff` (structural safety, network IDs, JWT auth, metrics).
**Do not re-apply the wave 1 worktrees** — their changes are already integrated. Stop/remove them after review.

## How to run checks

```bash
dotnet build Genelife.UnitTests/Genelife.UnitTests.csproj --nologo
dotnet run --project Genelife.UnitTests/Genelife.UnitTests.csproj --no-build --no-launch-profile
```

`dotnet test` fails on this project: xunit.v3 uses Microsoft.Testing.Platform, which .NET 10 no longer
runs through the VSTest target. Use the `dotnet run` command above.

## Conventions

- Base every worktree on `refactor/arch`; use the tracked path casing `Genelife.API`.
- One agent owns a shared file per wave. Shared/hot files:
  `SimulationEngine.cs`, `SimulationManager.cs`, `Program.cs`, `Genelife.UnitTests.csproj`.
- Worktree agents implement + test only; the coordinator integrates into `refactor/arch`,
  resolves shared-file conflicts, reruns build/tests, and commits once per wave.
- Never re-apply an already-integrated worktree; branches may be stale.
- Target NPC scale for v1: thousands of NPCs at ~25 Hz; 32 players on one world.

## Wave 1 — done

| Step | Status |
|---|---|
| MP01 query-safe structural changes | integrated |
| MP03 stable network IDs | integrated |
| MP07 identity and roles | integrated |
| Perf01 baseline instrumentation | integrated |

Integration fixes already applied: `CompanyRepository.Delete(NetworkId)` via registry, `DeleteCompany(ulong)`,
`CompanyEndpoints` auth + networkId routes, unified test csproj, tests adapted to `NetworkId`,
API directory normalized to `Genelife.API`.

## Wave 2 — tick loop, employment truth, perf baseline

| Agent | Steps | Owns | Depends |
|---|---|---|---|
| W2-A | MP02 employment single source of truth | `EmploymentIndex.cs`, `HiringSystem.cs`, `CompanyRepository` delete cleanup, tests | Wave 1 |
| W2-B | MP04 fixed-step tick loop | `SimulationEngine.cs`, new `SimulationLoop.cs`, `SimulationManager.cs`, `Program.cs` | Wave 1 |
| W2-C | Perf02 load harness + Perf03 zero-alloc decisions | new harness/fixtures, `DecisionSystem.cs`, tests; do not touch `SimulationEngine.cs`/`Program.cs` | Wave 1 |

- MP04 and MP05 are sequential: start **MP05 command queue** (W2-D) only after W2-B merges.
  MP05 owns `SimulationEngine.cs`, new `Commands/*`, mutation endpoints.
- Merge order: W2-A → W2-B → W2-D, then W2-C (or W2-C in parallel; it must not edit shared engine files).
- Acceptance: no ghost employees after company delete; fixed-step loop has bounded catch-up and real
  `deltaTime`; decision update allocates ~0; harness reports 1k/5k/10k fixtures.

## Wave 3 — snapshots, control split, scheduling

| Agent | Steps | Owns | Depends |
|---|---|---|---|
| W3-A | MP06 snapshot read model | new `Snapshots/*`, `SimulationEngine.cs`, `SimulationManager.cs`, read endpoints | MP03, MP05 |
| W3-B | MP08 player vs NPC control | new `PlayerControlled` component, `DecisionSystem.cs`, `SimRepository.cs` | MP07 |
| W3-C | Perf04 de-tick hiring + Perf05 de-tick job seeking | `HiringSystem.cs`, `JobSeekerSystem.cs`, cadence wiring; coordinate engine edits with W3-A | MP02, MP04 |
| W3-D | Perf06 snapshot-backed reads | read repositories/manager only; depends on W3-A | MP06 |

- W3-A is the only engine/manager owner this wave; W3-C schedules systems through the registry if it exists.
- Merge order: W3-A → W3-D, W3-B, W3-C.
- Acceptance: reads never take the tick lock; player Sims never auto-decide; hiring/job-seeking are off the hot path.

## Wave 4 — auth ownership, realtime, persistence, 2D

| Agent | Steps | Owns | Depends |
|---|---|---|---|
| W4-A | MP09 ownership + command authorization | `Commands/*`, auth, `SimulationEngine.cs` | MP08 |
| W4-B | MP10 SignalR transport | `Program.cs`, new `Hubs/*`, command submission | MP06, MP09 |
| W4-C | MP12 2D world + spatial grid + AOI | `Location.cs`, new `Spatial/*`, snapshot filtering | MP03, MP06 |
| W4-D | MP13 persistence and lifecycle | new `Persistence/*`, `SimulationManager.cs`, `Program.cs` | MP06 |

- W4-B and W4-D both touch `Program.cs`; sequence W4-B → W4-D or give one owner.
- Merge order: W4-A → W4-B → W4-D, W4-C in parallel if it avoids engine/manager.
- Acceptance: cross-player intent rejected; 32 clients receive filtered snapshots; restart resumes world;
  no `Vector3` in simulation code.

## Wave 5 — delta replication, acceptance, triggered scaling

| Agent | Steps | Owns | Depends |
|---|---|---|---|
| W5-A | MP11 delta snapshots + reconnect | `Snapshots/SnapshotHistory.cs`, hub | MP10 |
| W5-B | MP14 acceptance + 32-client load test | test harness, docs/plan results | MP11, MP12, MP13 |
| W5-C | Perf07 harden tick loop | `SimulationLoop.cs`, engine timing | Perf01, MP04 |
| W5-D | Perf08 registry/cadence + Perf09 CI guardrails | `ISimulationSystem`, registry, metrics, CI | Perf04, Perf05 |
| W5-E | Perf10 data hygiene + Perf11 pass reduction | components, systems, repositories | Perf03, Perf08 |
| W5-F | Perf12 triggered scaling | spatial/AOI/parallel only on measured trigger | Perf11 |

- Merge order: W5-A → W5-B; perf steps can run alongside but must not edit the same engine files.
- Perf12 is trigger-gated. Do not start without a recorded trigger and baseline.

## Startup checklist for the next agent

1. Confirm `git log -1` is `a48e3ff` (or later) and the tree is clean.
2. Create worktrees from `refactor/arch`; use the branch names above (`wave2/...`, etc.).
3. Prompt each agent with its step file path under `docs/plans/`, the owned files, and the check commands.
4. Tell agents: do not commit; leave changes in the worktree.
5. Integrate in the stated order, rerun build/tests, and commit once per wave.
6. Stop/remove worktrees after integration; never re-apply an integrated worktree.

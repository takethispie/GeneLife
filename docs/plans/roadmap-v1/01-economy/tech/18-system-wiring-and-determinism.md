# E18 — System wiring and determinism

**Phase:** 10 — Plumbing
**Depends:** E02, E07, E11, E12
**Estimate:** 1–2 sessions
**Status:** Implemented (V1)

## Goal

Wire the economy systems into `SystemRegistry` with a documented order and cadence, remove non-deterministic randomness, and keep the tick allocation budget.

## Context

Systems are registered explicitly in `SimulationEngine.Initialize` (`SimulationEngine.cs:91`). `SystemRegistry` rejects duplicate orders and names and measures per-system time/allocation. `SystemRegistryTests` and `PerformanceGuardrailTests` already enforce some of this. Several planned systems need random choices (attribution, scenario variance), and `Random.Shared` would make runs unreproducible.

## Design

### Registration

Use the order table in the root README. Economy additions:

| Order | System | Cadence |
|---|---|---|
| 400 | `HiringSystem` (modified) | 25 |
| 450 | `CompanyRevenueSystem` | 300 |
| 460 | `ConsumerSpendingSystem` | 100 |
| 500 | `PayrollSystem` (rewritten) | 300 |
| 530 | `BankingSystem` | 300 |
| 535 | `CorporateTaxSystem` | 300 |
| 540 | `CompanySolvencySystem` | 300 |
| 538 | `CreditSystem` | 300 |
| 610 | `EconomyMetricsSystem` | 1200 |
| 600 | `JobSeekerSystem` (modified) | 100 |

Order rationale: revenue before payroll before spending consequences? No — spending is demand collected over the cycle; revenue settles before payroll so cash is available on payday. Spending runs monthly and only books demand; it does not need payroll cash from the same tick. Rent and pension run after payroll.

### Randomness

```csharp
public sealed class SimulationRandom
{
    public SimulationRandom(int seed);
    public Random For(string streamName); // stable hash of stream name
}
```

- One stream per system: `"revenue.attribution"`, `"education.curriculum"`, `"mortality"`, `"population.inflow"`.
- Seed comes from world configuration and is persisted in the snapshot.
- Tests inject a fixed seed and assert exact outcomes.

### Allocation budget

- No LINQ in per-entity loops.
- Reuse `List<T>` buffers across ticks where the system is cadence-1.
- No string interpolation unless logging.
- Snapshot/read-model building stays outside `Tick` when it can lag.

### Cadence corrections

- `HiringSystem` stays at 25.
- `JobSeekerSystem` cadence changes from 100 to clock-month (100) and its age check becomes stage-based in life stages E06.
- `PayrollSystem` remains 300.

## Tasks

- [ ] Register all economy systems in `SimulationEngine.Initialize`.
- [ ] Add `SimulationRandom` and replace every `Random.Shared` in economy and shared code.
- [ ] Extend `SystemRegistryTests` to assert the canonical order is unique and complete.
- [ ] Add per-system allocation baselines to `PerformanceGuardrailTests`.
- [ ] Persist and restore the random seed.
- [ ] Add a debug invariant pass after each cadence cycle in test builds.
- [ ] Remove the legacy `EnqueuedCommand` string path once E17 commands cover all mutations.

## Files

- `Genelife.API/Services/SimulationEngine.cs`
- new `Genelife/SimulationRandom.cs`
- `Genelife/Systems/SystemRegistry.cs`
- `Genelife.UnitTests/Systems/SystemRegistryTests.cs`
- `Genelife.UnitTests/Performance/PerformanceGuardrailTests.cs`
- `Genelife.API/Persistence/PersistedWorldState.cs`

## Acceptance

- All economy systems run in the documented order and cadence.
- Two runs with the same seed produce identical balances and events.
- Changing the seed changes outcomes without breaking invariants.
- No economy system uses `Random.Shared`.
- 2,000-sim economy tick stays within the existing budget and allocation ceiling.
- Registry duplicate detection still works.

## Testing

- Determinism test: same seed → same money supply after 10 years.
- Order/cadence assertion table.
- Allocation guardrail test.
- Seed persistence round-trip test.

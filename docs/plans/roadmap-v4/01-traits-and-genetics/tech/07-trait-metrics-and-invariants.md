# T07 — Trait metrics and invariants

**Phase:** 7 — Observability
**Depends:** T01–T06
**Estimate:** 1 session
**Status:** Done

## Goal

Make missing, invalid or collapsing trait state visible before it breaks attraction or inheritance.

## Context

The lifecycle and economy systems already expose health checks and yearly metrics. Traits need the same treatment: a distribution summary, a mutation rate, a missing-trait count and parent-child similarity once relationships exist.

## Design

### Invariants

`TraitInvariants.Verify(world)` returns a list of issues:

- living sim without `Traits`;
- undefined enum value;
- trait component on a dead/destroyed entity that should not have one;
- duplicate `Traits` component (impossible in ECS but assert).

Levels:

| Condition | Level |
|---|---|
| living sim missing traits | Fail |
| undefined enum | Fail |
| distribution collapsed to one value for a trait over the whole population | Warn |
| mutation rate outside `[0.01, 0.10]` over the last year | Warn |
| no traits generated yet | Warn |

### Distribution

`TraitDistribution.Calculate(world)` returns counts and shares per trait value, plus:

- population size;
- sex ratio;
- mutation events this year;
- migrated sim count;
- parent-child similarity once feature 02 exists.

### Read surface

- Add trait fields to `SimLifecycleStatus` for the admin Data/Lifecycle pages.
- Add a `TraitStatus` block to the family read model in feature 05.
- Expose health checks through the existing `/api/lifecycle/health` and the new `/api/family/health`.

### Cadence

- Invariants run on demand for the read model and once per year for the health snapshot.
- Distribution is cheap enough to compute on demand for 2,000 sims; no new tick system is required.

## Tasks

- [ ] Add `TraitInvariants`.
- [ ] Add `TraitDistribution`.
- [ ] Add trait fields to the lifecycle read model.
- [ ] Add health checks to the engine.
- [ ] Add mutation counters to the yearly snapshot.
- [ ] Add tests for each invariant and distribution edge.

## Files

- new `Genelife/Family/TraitInvariants.cs`
- new `Genelife/Family/TraitDistribution.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.Contracts/Dtos/LifecycleStatus.cs`
- `Genelife.UnitTests/Family/TraitMetricsTests.cs`

## Acceptance

- A living sim without traits is reported as a failure.
- An undefined enum is reported as a failure.
- Distribution counts sum to the living population for each trait.
- Sex ratio and mutation rate are visible.
- Health checks appear in the admin API with the correct level.

## Testing

- Remove a `Traits` component and assert a failure.
- Set an invalid enum and assert a failure.
- Generate two populations and compare distribution shape.
- Assert distribution totals for each trait.
- Assert the health endpoint includes trait checks.
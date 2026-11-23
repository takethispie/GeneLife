# C01 — Births vs immigration policy

**Phase:** 1 — Continuity
**Depends:** feature 03 births, `PopulationInflowSystem`, `PopulationSnapshot`
**Estimate:** 1–2 sessions
**Status:** Planned (V4)

## Goal

Treat the population target as a floor and let births and immigration both contribute, without one-for-one replacement or a hidden cap.

## Context

`PopulationInflowSystem` currently:

- sets `TargetPopulation` to the seeded living population;
- admits up to 2% of the population per year, with a minimum of 5;
- pauses when unemployment exceeds 10%, housing vacancy is zero, or the working-age share is below 55%;
- queues arrivals when no unit is available.

`PopulationSnapshot` records deaths and arrivals but not births.

## Design

### Target semantics

- `TargetPopulation` is a **floor**.
- `gap = max(0, TargetPopulation - LivingPopulation)`.
- Immigration continues to fill the gap subject to the existing pause checks.
- Births may push the population above the target; do not raise the target automatically.
- If births alone close the gap, immigration still pauses when unemployment or housing checks fail.
- A population above target is allowed to grow; the target is not a cap.

### Birth accounting

Add to `LifeContext`:

```csharp
public int BirthsThisYear { get; set; }
```

Reset on the yearly boundary, increment on every completed birth, and include it in `PopulationSnapshot`.

Add to `PopulationSnapshot`:

- `BirthsThisYear`;
- `BirthRate` per 1,000;
- `NaturalChange` (births − deaths);
- `NetMigration`;
- `ReplacementRate`.

### Immigration interaction

- Do not subtract births one-for-one from the arrival budget.
- Use the gap, not a headcount swap.
- If the gap is zero, immigration pauses with reason `TargetReached`, even if the working-age share is healthy.
- If the gap is positive but births are expected to close it, the arrival budget may be reduced; V4a keeps the simple rule: fill the observed gap and let births overshoot.
- Pending arrivals are counted against the gap when deciding whether to admit more.

### Age composition

Births add children first. The policy must not treat a newborn as a worker:

- immigration continues to target working-age adults;
- the working-age share check remains;
- the dependency ratio is reported, not used to block births.

## Tasks

- [ ] Add `BirthsThisYear` and reset it yearly.
- [ ] Increment it from `BirthSystem`.
- [ ] Add birth fields to `PopulationSnapshot`.
- [ ] Add the `TargetReached` pause reason.
- [ ] Count pending arrivals against the gap.
- [ ] Keep the existing unemployment/housing/working-age checks.
- [ ] Add tests for births, gap, target floor and pause reasons.

## Files

- `Genelife/Life/LifeContext.cs`
- `Genelife/Life/PopulationSnapshot.cs`
- `Genelife/Systems/PopulationInflowSystem.cs`
- `Genelife/Systems/PopulationMetricsSystem.cs`
- `Genelife/Life/ImmigrationPolicy.cs`
- `Genelife.UnitTests/Lifecycle/ImmigrationTests.cs`

## Acceptance

- Births are counted in the yearly snapshot.
- The target acts as a floor, not a cap.
- Immigration pauses at or above target with `TargetReached`.
- Births do not cancel arrivals one-for-one.
- Pending arrivals reduce further admissions.
- Existing pause reasons still work.

## Testing

- Target above population: arrivals continue.
- Births close the gap: target reached, immigration pauses.
- Births push above target: population grows, no correction.
- Pending arrivals count against the gap.
- Replay a year and compare arrivals and births.
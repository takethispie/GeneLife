# F02 — Conception system

**Phase:** 2 — Conception
**Depends:** F01, R03, R06
**Estimate:** 1–2 sessions
**Status:** Done

## Goal

Create pregnancies deterministically for eligible partners at a believable rate without minting children for every couple every month.

## Context

Partners exist after feature 02. Conception is the first system that turns a relationship into a family outcome. It must respect age, health, existing pregnancy, partnership state and the configured carrying-parent policy.

## Design

### Options

```csharp
public sealed class ReproductionOptions
{
    public int MinimumConceptionAge { get; set; } = 18;
    public int MaximumConceptionAge { get; set; } = 45;
    public int MinimumConceptionHealth { get; set; } = 50;
    public decimal MonthlyConceptionChance { get; set; } = 0.03m;
    public decimal MonthlyLossChance { get; set; } = 0.01m;
    public decimal LossCooldownMonths { get; set; } = 3m;
    public int PregnancyMonths { get; set; } = 9;
    public Money MonthlyPrenatalCareCost { get; set; } = Money.FromUnits(40m);
    public bool AllowSameSexCarryingPair { get; set; } = false;
}
```

### Carrying parent policy

- V4a defaults to a female carrying parent.
- Same-sex pairing is allowed, but conception requires a carrying-capable pair unless `AllowSameSexCarryingPair` is explicitly enabled with a future mechanism.
- Admin can cancel or force a pregnancy through a typed command; forcing is not part of the natural loop.

### System

`ConceptionSystem` at order 370, cadence 100 (monthly):

1. Collect active partnerships whose carrying parent is eligible.
2. Exclude minors, dependants, dead sims, existing pregnancies and a loss cooldown.
3. Exclude pairs with health below `MinimumConceptionHealth`.
4. For each remaining pair, in stable `NetworkId` order:
   - derive a pair-keyed `Random` from `reproduction.conception`, both network ids and the year/month;
   - roll `MonthlyConceptionChance`;
   - on success, create the pregnancy entity and index entry.
5. Emit `PregnancyStarted` and `ConceptionOccurred`.

### Determinism

- The roll is keyed by the canonical pair and the tick, not by iteration order.
- Adding a new pair or a new candidate must not change other pairs' rolls.
- No `Random.Shared`, wall-clock time or float comparison.

### Rate tuning

- The monthly chance is a balance lever.
- The initial target is a birth rate that replaces the population alongside immigration, not a baby boom.
- The 10-year balance scenario measures births, deaths and population change.

## Tasks

- [ ] Add `ReproductionOptions` and validation.
- [ ] Add `ConceptionSystem` with stable pair-keyed rolls.
- [ ] Apply carrying-parent and health eligibility.
- [ ] Create the pregnancy entity, index entry and events.
- [ ] Add loss cooldown storage.
- [ ] Add deterministic rate and replay tests.
- [ ] Add the balance hook for births.

## Files

- new `Genelife/Family/ReproductionOptions.cs`
- new `Genelife/Systems/ConceptionSystem.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.UnitTests/Family/ConceptionTests.cs`

## Acceptance

- An eligible partnered pair can conceive once and only once.
- A minor, dependent, dead or already-pregnant carrying parent never conceives.
- A pair below the health threshold never conceives.
- The same seed and month produce the same conception set.
- The observed rate over 1,000 pair-months is within tolerance of the configured chance.
- A loss cooldown blocks conception until it expires.

## Testing

- Table-driven eligibility failures.
- 1,000 pair-months with a fixed seed and rate assertion.
- Replay the same seed and compare pregnancy ids.
- Conceive, lose, and assert the cooldown.
- Assert a second active pregnancy is impossible.
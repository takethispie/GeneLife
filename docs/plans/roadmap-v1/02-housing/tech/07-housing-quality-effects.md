# H07 — Housing quality effects

**Phase:** 4 — Quality
**Depends:** H05, H06
**Estimate:** 2 sessions
**Status:** Implemented (V1)

## Goal

Make housing quality change how the population lives: sleep, hygiene, health and overcrowding effects that are cheap to evaluate and visible to the admin.

## Context

`NeedsDecaySystem` applies fixed rates (`Genelife/Systems/NeedsDecaySystem.cs`) and `ActionSystem` restores fixed amounts regardless of where a sim lives. Quality must not add a query per tick.

## Design

A derived component on each sim:

```csharp
public record struct NeedsModifier
{
    public float HungerDecay;
    public float EnergyDecay;
    public float HygieneDecay;
    public float BladderDecay;
    public float SleepRecovery;
    public float HygieneRecovery;
    public float HealthDrift;
    public bool NoUtilities;
    public bool Overcrowded;
}
```

`HousingModifierSystem` order 80, cadence 100:

- resolve `Residence` → unit → property;
- compute multipliers from tier, condition, neighbourhood, occupancy, utility state;
- write `NeedsModifier` only if values changed (dirty check);
- no allocation per sim.

Base table:

| Tier | Energy decay | Hygiene decay | Sleep recovery | Health drift |
|---|---|---|---|---|
| Shelter | 1.50× | 1.75× | 0.50× | −0.4 / month |
| Basic | 1.00× | 1.00× | 0.80× | 0.0 |
| Comfortable | 0.90× | 0.85× | 1.00× | +0.1 |
| Luxury | 0.80× | 0.75× | 1.15× | +0.2 |

Modifiers stack multiplicatively:

- condition below 0.5 → hygiene and health worsen linearly;
- neighbourhood score below 0.4 → health drift −0.1;
- `NoUtilities` → hygiene ×1.5, health drift −0.2;
- overcrowded → sleep recovery ×0.8, hygiene decay ×1.25, health drift −0.2;
- homeless → use shelter row with an additional health penalty.

`NeedsDecaySystem` multiplies its rates by `NeedsModifier` when present, defaulting to 1.0. `ActionSystem` multiplies restoration by `SleepRecovery`/`HygieneRecovery`. `HealthSystem` (life stages) adds `HealthDrift`.

## Tasks

- [ ] Add `NeedsModifier` with a neutral default.
- [ ] Implement `HousingModifierSystem` with dirty checks.
- [ ] Update `NeedsDecaySystem` and `ActionSystem` to apply modifiers.
- [ ] Update `HealthSystem` to consume `HealthDrift`.
- [ ] Recompute modifiers on move, eviction, utility change, condition change and occupancy change.
- [ ] Add debug assertions that every sim with a home has a modifier.
- [ ] Expose quality, overcrowding and utility state in `SimStatus`.

## Files

- new `Genelife/Components/Gen/NeedsModifier.cs`
- new `Genelife/Systems/HousingModifierSystem.cs`
- `Genelife/Systems/NeedsDecaySystem.cs`, `ActionSystem.cs`
- `Genelife.API/DTOs/SimStatus.cs`

## Acceptance

- Two sims with identical needs decay at different rates in different tiers.
- Sleep and hygiene recovery match the tier table.
- No-utilities and overcrowding produce the documented penalties.
- Modifier updates happen on move/condition change without a full scan.
- Removing a home switches the sim to the homeless profile.
- Performance impact is below the per-tick budget at 2,000 sims.

## Testing

- Tier multiplier matrix.
- Stacking test (condition × utilities × overcrowding).
- Move-updates-modifier test.
- Homeless profile test.
- Allocation guardrail.

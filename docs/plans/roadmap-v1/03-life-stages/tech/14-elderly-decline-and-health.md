# L14 — Elderly decline and health

**Phase:** 6 — Elderly
**Depends:** L11, H07, E13
**Estimate:** 2–3 sessions
**Status:** Planned

## Goal

Model health as the bridge between age, housing, money and death, and make the elderly stage a gradual decline rather than a fixed timer.

## Context

`Needs` are binary resources that can always be restored. Nothing connects housing quality, poverty or age to long-term condition. Mortality needs a health signal, and the admin needs to see why a population is dying.

## Design

### Health

```csharp
public record struct Health
{
    public float Value;          // 0..100
    public float DeclineRate;    // per month, derived
    public long LastCareTick;
    public int ChronicConditions;
}
```

`HealthSystem` order 95, cadence 100 (monthly), for all living sims:

```
drift = baseDrift(age)
      + housingDrift(NeedsModifier.HealthDrift)
      + needsDrift(mean of critical needs)
      + povertyDrift(household wealth)
      + careDrift(recent medical purchase)
```

- `baseDrift`: 0 below 50, negative and accelerating after 65, steep after 80.
- `needsDrift`: negative when any need is below 20 for the month.
- `povertyDrift`: negative when household cash < one month of expenses.
- `careDrift`: positive for one month after a medical purchase, scaled by quality.
- Health is clamped `[0, 100]`; 0 is a mortality trigger, not immediate death.

### Care

Households buy health care through the economy consumption basket (`Health` category). The purchase creates a `MedicalCarePurchased` event consumed by `HealthSystem`. The government funds a clinic program for households below a poverty threshold, with capacity and a budget.

### Elder decline

`ElderDeclineSystem` order 560, cadence 100, for 80+:

- increases `NeedsModifier` decay and reduces recovery further;
- adds a small monthly health decline;
- reduces shift/action efficiency (elders do not work anyway);
- increases the chance of a care need event.

Decline factor:

```
f = clamp((ageTicks / ticksPerYear − 80) / 25, 0, 1)
energyRecovery ×= 1 − 0.4f
hygieneRecovery ×= 1 − 0.3f
healthDrift −= 0.25f per month
```

### Health effects

- low health reduces action effectiveness and increases shift interruptions for workers;
- health below 30 raises mortality risk and flags a care need;
- health is visible in `SimStatus` and the admin.

## Tasks

- [ ] Add `Health` to all living sims and initialise by age.
- [ ] Implement `HealthSystem` drift with the documented inputs.
- [ ] Implement medical care purchase and clinic program.
- [ ] Implement `ElderDeclineSystem` modifiers.
- [ ] Emit `HealthDeclined`, `MedicalCarePurchased`, `CareNeeded`.
- [ ] Expose health, decline rate and care status in the admin.
- [ ] Add admin actions to restore health or trigger a care need.
- [ ] Ensure action/shift systems consume health efficiency.

## Files

- `Genelife/Components/Life/Health.cs`
- new `Genelife/Systems/HealthSystem.cs`, `ElderDeclineSystem.cs`
- `Genelife/Systems/NeedsDecaySystem.cs`, `ActionSystem.cs`, `WorkScheduleSystem.cs`
- `Genelife.Messages/Events/Health*.cs`, `MedicalCarePurchased.cs`
- `Genelife.API/DTOs/SimStatus.cs`

## Acceptance

- Health declines with age and improves with care.
- Poor housing, unmet needs and poverty accelerate decline.
- Elders decline faster than younger adults with the same inputs.
- Low health increases mortality risk and reduces action efficiency.
- Care purchases are funded and visible.
- Health is persisted and restored.

## Testing

- Drift formula matrix by age/housing/needs/poverty.
- Care restoration test.
- Elder scaling test.
- Mortality-risk correlation test.
- Restore round-trip test.

# Mortality

Genes die from two causes: stochastic mortality (Elder stage only) and needs-depletion death (any stage, any age). Death triggers inheritance and entity cleanup.

## Mortality System (`MortalitySystem`)

- **Cadence:** Every 600 ticks (~24 seconds at 25 Hz)
- **Query:** `Elderly` + `Alive` + `Wallet` + `Needs`

### Stochastic Death

Each cycle, each Elder Gene has a chance to die based on age, wealth, and needs status:

```csharp
float ComputeMortalityChance(int age, float walletBalance, Needs needs) {
    float ageFactor = (age - 80) * 0.005f;             // 0 at 80, 0.1 at 100
    float wealthFactor = Math.Max(0, 1f - walletBalance / 5000f);
    float needsFactor = (100f - Math.Min(needs.Hunger, needs.Energy)) / 100f; // Low needs = higher risk

    float chance = 0.005f                              // Base: 0.5% per check
        + ageFactor * 0.5f                             // Age: up to +5% at 100
        + wealthFactor * 0.02f                         // Poverty: up to +2%
        + needsFactor * 0.03f;                         // Needs: up to +3%

    return Math.Min(chance, 0.15f);                    // Cap at 15% per check
}
```

Example probabilities (per 600t check):
- Age 80, wealthy, full needs: ~0.5%
- Age 90, broke, low needs: ~8.5%
- Age 100, broke, starving: ~15% (capped)

## Needs-Depletion Death (Any Stage)

`NeedsDeathSystem` runs every tick:

- **Query:** `Needs` + `Alive` (all life stages)
- If any need (Hunger, Energy, Hygiene, Bladder) reaches **exactly 0**:
  - Flag entity for death
  - This represents starvation, exhaustion, disease

More precisely, death only triggers if ALL of these are true for a Gene:
1. A need is at 0
2. `CurrentAction.Type == Idle` (not attempting to fulfill the need)
3. No wallet balance to buy food/services (if applicable)

This prevents death during an action that's restoring the depleted need. The system rechecks each tick — if a Gene at 0 Hunger is Eating (action restoring Hunger), they survive. If they're Idle with Hunger at 0 and can't eat, they die.

## Death Processing

When a Gene dies (from either cause):

```csharp
void ProcessDeath(Entity deceased) {
    // 1. Queue inheritance event
    inheritanceQueue.Enqueue(new DeathEvent {
        DeceasedEntity = deceased,
        WalletBalance = world.Get<Wallet>(deceased).Balance,
        SavingsAmount = world.Get<Savings>(deceased).SavedBalance,
        TickOfDeath = currentTick
    });

    // 2. Clean employment records
    if (world.Has<Employee>(deceased)) {
        employmentIndex.Remove(deceased);
    }

    // 3. Remove from spatial index
    spatialIndex.Remove(deceased);

    // 4. Destroy entity (or flag as dead for one tick for snapshot purposes)
    world.Destroy(deceased);
}
```

The destruction is deferred by one tick so snapshot systems can capture the death event.

## Death Statistics

`MortalitySystem` tracks:
- Deaths per cycle
- Cause breakdown (stochastic vs needs-depletion)
- Average age of death
- Average wealth at death

Stored in `DemographicsSnapshot` alongside population stats.

## Preventing Mass Extinction

V1 does not replace dead Genes. The population naturally declines over time. For long-running simulations, this is acceptable — testing will surface the decline rate and inform future versions about birth/immigration mechanics.

If the population drops below a configurable minimum (`MinPopulation = 10` by default), `PopulationHealthSystem` fires a warning. No corrective action in V1.

## Acceptance Criteria

- Elder Genes have a stochastic death chance every 600 ticks
- Death probability increases with age, poverty, and declining needs
- Needs-depletion death triggers when a need hits 0 and no action is restoring it
- Death cleans up employment records and spatial index
- Death queues an inheritance event
- Entity is destroyed within 2 ticks of death flagging
- Death statistics are logged to DemographicsSnapshot
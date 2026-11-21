# Elder Stage (Age 80+)

The terminal life stage. Elder Genes have no income, declining vitality, and face mortality risk. This stage ends with death and triggers inheritance distribution.

## Stage Entry

- Triggered by `StageProgressionSystem` when `Alive.Age >= 80`
- Components added: `Elderly`
- Components removed: `Pensioner` (pension income stops)
- Wallet balance frozen — Elder Genes spend from remaining savings only

## Mortality System (`MortalitySystem`)

- **Cadence:** Every 600 ticks (~24 seconds at 25 Hz)
- **Query:** `Elderly` + `Alive`

### Mortality Probability

Every cadence cycle, each Elder Gene rolls for death:

```csharp
float baseChance = 0.01f;              // 1% per check
float ageFactor = (alive.Age - 80) * 0.005f; // +0.5% per year over 80
float wealthFactor = Math.Max(0, 1.0f - wallet.Balance / 5000f); // poorer = higher risk, max 1.0x

float mortalityChance = baseChance + ageFactor * wealthFactor;
// At age 90 with empty wallet: ~6% per check
// At age 80 with good savings: ~1% per check
```

When an Elder dies:
1. `MortalitySystem` queues a death event (don't destroy entity mid-query)
2. `InheritanceSystem` processes the event in the next tick

## Decline Mechanics

Elder Genes experience growing difficulty:
- Needs decay at 1.2× normal rate (aging bodies)
- Needs restoration from actions is 50% effective (Eating restores 4/s instead of 8/s)
- `DecisionSystem` critical threshold increases from 30 to 50 (more reactive to needs)

These are applied by `ElderDeclineSystem` which runs every tick and scales effects with `Alive.Age - 80`.

### Decline Scaling

```csharp
float declineFactor = Math.Min(1.0f, (alive.Age - 80) / 20f); // 0 at 80, 1 at 100
float decayMultiplier = 1.0f + declineFactor * 0.5f;           // 1.0 → 1.5
float restorationMultiplier = 1.0f - declineFactor * 0.5f;    // 1.0 → 0.5
float criticalThreshold = 30f + declineFactor * 40f;           // 30 → 70
```

At age 80: normal decay, normal restoration, critical at 30.
At age 90: 25% increased decay, 25% reduced restoration, critical at 50.
At age 100: 50% increased decay, 50% reduced restoration, critical at 70.

This creates a natural death-by-needs: eventually, needs decay faster than they can be restored, and a Gene dies from starvation/energy depletion even without the mortality check.

## Components

| Component | Fields | Notes |
|---|---|---|
| `Elderly` | (marker) | Triggers MortalitySystem and ElderDeclineSystem |

## Acceptance Criteria

- At age 80, Gene transitions to Elder; pension stops
- `MortalitySystem` checks for death every 600 ticks
- Mortality probability increases with age and decreases with wealth
- Elder Genes have accelerated needs decay and reduced restoration
- A Gene reaching 0 in any need with no restoration possible dies naturally
- Death triggers inheritance processing (see [`04-life-events/inheritance.md`](../04-life-events/inheritance.md))
# Consumer Spending

Every Gene with a `Wallet` periodically spends money. Spending is abstracted — no inventory, no specific product selection. It represents aggregate consumption (food, goods, services).

## Consumer System (`ConsumerSystem`)

- **Cadence:** Every 100 ticks (~4 seconds at 25 Hz)
- **Query:** `Wallet` + `LifeStage` + `Alive`

### Spending Calculation

Each cycle, subtract a spending amount based on life stage:

```csharp
float ComputeSpending(LifeStage stage, float walletBalance) {
    float baseSpending = stage switch {
        LifeStage.Student => 30f,   // Low consumption, mostly food
        LifeStage.Worker   => 80f,   // Full consumption
        LifeStage.Retired  => 50f,   // Reduced consumption
        LifeStage.Elder    => 40f,   // Minimal consumption
        _ => 0f                     // Children handled separately
    };

    // If wallet can't cover spending, spend all remaining
    return Math.Min(baseSpending, walletBalance);
}
```

### Spending Sources

Each Gene's spending is attributed to:
1. A random company (weighted by company employee count — bigger companies get more business)
2. If no companies exist, money is destroyed (represents informal economy)

Attribution is needed so `RevenueSystem` can route consumer money to company wallets.

### Spending Record

New component: `SpendingRecord` (record struct)

```csharp
struct SpendingRecord {
    float TotalSpent;       // Lifetime spending
    float LastCycleSpent;   // Most recent cycle
    float UnattributedAccum; // Spending waiting for company attribution
}
```

Attached to each Gene entity. Updated every consumer cycle.

### Wallet Floor

Genes cannot spend below zero. If `walletBalance < baseSpending`:
- Spend whatever is available
- Trigger a "poverty" state (future versions: reduced health, unhappiness)
- V1: just log and continue; Gene will struggle to meet needs

## Student Spending

Students receive a 50f stipend per 300t (from Education stage). They spend 30f per 100t, meaning they operate at a deficit without work — this is intentional: student wallets drain slowly, creating urgency to graduate and find work.

## Economic Behavior by Stage

| Stage | Income Source | Spending Rate | Net per Cycle |
|---|---|---|---|
| Student | Stipend (50f/300t = ~17f/100t) | 30f/100t | **-13f** (deficit) |
| Worker | Salary (~800-1500/300t) + wage | 80f/100t | **+190 to +420** (surplus) |
| Retired | Pension (~300-600/300t) | 50f/100t | **-50 to +150** (depends on pension size) |
| Elder | Savings only | 40f/100t | **-40f** (pure deficit) |

Workers accumulate savings. Retirees break even or slowly deplete. Students and Elders drain. This creates natural economic pressure — a Gene must build sufficient savings during Work to survive Retirement and Elder stages.

## Acceptance Criteria

- `ConsumerSystem` deducts from Wallet every 100 ticks based on stage
- Spending is attributed to random companies for revenue tracking
- Genes cannot spend below zero
- Students run a deficit; Workers run a surplus
- A Gene with zero balance stops spending but continues to exist
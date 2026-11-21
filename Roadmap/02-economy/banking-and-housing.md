# Banking, Housing, and Wealth

Genes accumulate wealth during their Work stage and spend it down during Retirement and Elder stages. Banking tracks long-term savings separate from the spendable wallet. Housing represents a recurring cost tied to economic status.

## Banking System (`BankingSystem`)

- **Cadence:** Every 300 ticks (aligned with PayrollSystem)
- **Query:** `Wallet` + `LifeStage`

### Savings Behavior

At the end of each pay cycle, if a Gene's `Wallet.Balance` exceeds a buffer, excess moves to savings:

```csharp
float spendingBuffer = lifeStage.Stage switch {
    LifeStage.Worker   => 500f,  // Keep 500f in wallet for spending
    LifeStage.Retired  => 200f,  // Keep less — spending is lower
    LifeStage.Elder    => 100f,  // Keep minimal — income stopped
    _ => 0f
};

float excess = wallet.Balance - spendingBuffer;
if (excess > 0) {
    wallet.Balance = spendingBuffer;
    savings.SavedBalance += excess;
}
```

### Withdrawals

When a Gene's wallet falls below spending needs and savings exist, withdraw from savings:

```csharp
float deficit = baseSpending - wallet.Balance;
float withdrawal = Math.Min(deficit, savings.SavedBalance);
savings.SavedBalance -= withdrawal;
wallet.Balance += withdrawal;
```

This happens automatically in `ConsumerSystem` before spending is deducted.

### Savings Component

New component: `Savings` (record struct)

```csharp
struct Savings {
    float SavedBalance;     // Total accumulated savings
    float TotalEarned;      // Lifetime earnings (wages + pension)
    float TotalSpent;       // Lifetime spending
    float TotalSaved;       // Lifetime deposits into savings
}
```

Attached to each Gene at creation. Workers and Retirees with savings are tracked here.

## Pension Funding

During the Work stage, a portion of each paycheck is diverted to pension savings:

```csharp
// In PayrollSystem: after adding salary to wallet
float pensionContribution = employee.Salary * 0.05f; // 5% pension contribution
savings.TotalSaved += pensionContribution;
```

At retirement, total pension savings determine the pension amount:

```csharp
// At stage transition Worker → Retired
float pensionPool = savings.SavedBalance * 0.5f; // Use 50% of savings for pension
float monthlyPension = ComputePension(wallet, skills, pensionPool);
```

The remaining 50% of savings transfers to `RetirementSavings` for the Elder stage.

## Housing System (`HousingSystem`)

- **Cadence:** Every 600 ticks (~24 seconds at 25 Hz)
- **Query:** `Wallet` + `LifeStage` (adults only: Worker, Retired, Elder)

### Housing Tiers

Genes pay housing costs based on their wealth tier. Tiers are recalculated each cycle:

```csharp
enum HousingTier { Homeless, Basic, Comfortable, Luxury }

HousingTier ComputeHousingTier(Wallet wallet, Savings savings) {
    float totalWealth = wallet.Balance + savings.SavedBalance;
    return totalWealth switch {
        < 200f  => HousingTier.Homeless,    // No housing cost, but needs decay faster
        < 1000f => HousingTier.Basic,       // 100f/cycle
        < 5000f => HousingTier.Comfortable, // 300f/cycle
        _       => HousingTier.Luxury       // 600f/cycle
    };
}
```

### Housing Cost Effects

| Tier | Cost/Cycle | Effect |
|---|---|---|
| Homeless | 0f | Energy decay +50% (sleeping rough) |
| Basic | 100f | Neutral |
| Comfortable | 300f | Hygiene decay -20% (better facilities) |
| Luxury | 600f | Energy + Hygiene decay -30% |

Housing costs are deducted from `Wallet.Balance`. If the Gene can't afford it, they drop to the next lower tier automatically.

### Housing Component

New component: `Housing` (record struct)

```csharp
struct Housing {
    HousingTier CurrentTier;
    float CostPerCycle;
    int TicksUntilNextPayment;
}
```

Attached to adult Genes. Updated by `HousingSystem`.

## Wealth Transfer at Death

When a Gene dies (Elder stage mortality or needs-depletion death):
1. If any `Savings.SavedBalance > 0`: transfer to a random living Gene (inheritance)
2. Destroy `Wallet`, `Savings`, `Housing` components
3. Destroy the entity

See [`04-life-events/inheritance.md`](../04-life-events/inheritance.md) for details.

## Economic Health Metrics

For debugging and balancing, track global metrics (optional for V1, but recommended):

```csharp
struct EconomyMetrics {
    float TotalMoneyInCirculation;   // Sum of all wallets + savings
    float TotalCompanyCapital;       // Sum of all company wallets
    float GiniCoefficient;           // Wealth inequality (0-1)
    int HomelessCount;
    int BankruptCompanyCount;
    float AverageWorkerSavings;
    float PensionCoverageRatio;      // Average pension / average spending
}
```

Store on a singleton entity or in a static metrics collector. Logged every 3600 ticks (2.4 min at 25 Hz) for tuning.

## Acceptance Criteria

- Workers accumulate savings when wallet exceeds buffer
- Savings are withdrawn when wallet runs low
- 5% of salary auto-contributes to pension pool during Work stage
- Housing costs are deducted every 600 ticks based on wealth tier
- Homeless Genes have accelerated Energy decay
- A Gene that can't afford housing automatically drops a tier
- At death, remaining savings transfer as inheritance
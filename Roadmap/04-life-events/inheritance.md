# Inheritance

When a Gene dies, any remaining wealth transfers to a living Gene. This keeps money in circulation and creates intergenerational economic effects.

## Inheritance System (`InheritanceSystem`)

- **Cadence:** Every 100 ticks (processes queued death events)
- **Input:** Queue of `DeathEvent` structs from `MortalitySystem`

### Death Event

```csharp
struct DeathEvent {
    Entity DeceasedEntity;    // Already destroyed — just for logging
    float WalletBalance;
    float SavingsAmount;
    ulong TickOfDeath;
}
```

### Heir Selection

Wealth is distributed to one or more living Genes. V1 uses a single random heir:

```csharp
Entity SelectHeir(World world) {
    var candidates = new List<Entity>();

    // Query all living adults (Worker/Retired/Elder)
    var query = new QueryDescription().WithAll<Alive, Wallet, LifeStage>();
    world.Query(in query, (Entity entity, ref LifeStage stage) => {
        if (stage.Stage == LifeStage.Worker ||
            stage.Stage == LifeStage.Retired ||
            stage.Stage == LifeStage.Elder) {
            candidates.Add(entity);
        }
    });

    if (candidates.Count == 0) {
        // No valid heirs — money is destroyed
        return Entity.Null;
    }

    // Random heir (weighted towards lower-wealth Genes)
    // Sort by wealth ascending, pick randomly with bias toward top 50%
    candidates.Sort((a, b) => {
        float wealthA = GetTotalWealth(a);
        float wealthB = GetTotalWealth(b);
        return wealthA.CompareTo(wealthB);
    });

    // Pick from the poorer half (50% chance), or completely random (50% chance)
    if (Random.Shared.NextDouble() < 0.5f) {
        int maxIndex = Math.Max(1, candidates.Count / 2);
        return candidates[Random.Shared.Next(maxIndex)];
    } else {
        return candidates[Random.Shared.Next(candidates.Count)];
    }
}
```

The bias toward poorer Genes means inheritances tend to reduce wealth inequality rather than concentrate it.

### Wealth Transfer

```csharp
void ProcessInheritance(DeathEvent death) {
    float totalInheritance = death.WalletBalance + death.SavingsAmount;

    if (totalInheritance <= 0f) return; // Nothing to inherit

    Entity heir = SelectHeir(world);
    if (heir == Entity.Null) {
        // Money destroyed — logged as economic drain
        return;
    }

    var heirWallet = world.Get<Wallet>(heir);
    var heirSavings = world.Get<Savings>(heir);

    // Split: half to wallet, half to savings
    float walletPortion = totalInheritance * 0.5f;
    float savingsPortion = totalInheritance * 0.5f;

    heirWallet.Balance += walletPortion;
    heirSavings.SavedBalance += savingsPortion;
}
```

### Inheritance Tax (Optional)

A configurable tax rate reduces the inheritance before transfer. Default V1: 0% (no tax). Can be set via `EconomicConfig.InheritanceTaxRate`.

```csharp
float afterTax = totalInheritance * (1f - InheritanceTaxRate);
// Tax money is destroyed (removed from economy)
```

Future versions may route tax revenue to a government budget.

## No-Heir Scenario

If no living adult Genes exist (everyone is a Child or Student):
- Money is destroyed
- Logged as a "wealth destruction" event
- This is normal during early simulation startup before Workers exist

## Inheritance Statistics

`InheritanceSystem` tracks:
- Total inheritances processed
- Average inheritance amount
- Number of no-heir events (wealth destroyed)
- Largest single inheritance

All metrics added to `DemographicsSnapshot`.

## Acceptance Criteria

- Death triggers inheritance processing within 100 ticks
- Heir is a living adult (Worker, Retired, or Elder)
- Heir selection is biased toward lower-wealth Genes
- Wealth splits 50% to heir's wallet, 50% to heir's savings
- If no valid heir exists, money is destroyed (logged)
- Inheritance tax rate is configurable (default 0%)
- Multiple deaths in a cycle are processed as a batch
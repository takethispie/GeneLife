# NPC Archetypes

While all Genes use the same systems, V1 defines a few archetype profiles to create behavioral variety. Archetypes influence decision-making thresholds, spending patterns, and job preferences — making the population feel diverse without adding complex AI.

## Archetype Component

```csharp
enum Archetype { Ambitious, Content, Frugal, Carefree, Industrious, Lazy }

struct GeneArchetype {
    Archetype Type;
    // Modifiers (multipliers, 1.0 = baseline)
    float WorkEthic;        // Affects shift duration, skill gain rate
    float SpendingRate;     // Affects ConsumerSystem spending
    float SavingRate;       // Affects BankingSystem deposit threshold
    float NeedsAwareness;   // Affects DecisionSystem critical threshold
    float JobAmbition;      // Affects DesiredSalary from JobSeeker
}
```

## Archetype Profiles

| Archetype | WorkEthic | Spending | Saving | NeedsAware | Ambition |
|---|---|---|---|---|---|
| **Ambitious** | 1.3 | 0.9 | 1.2 | 0.8 | 1.5 |
| **Content** | 1.0 | 1.0 | 1.0 | 1.0 | 0.8 |
| **Frugal** | 1.0 | 0.5 | 1.5 | 1.0 | 0.7 |
| **Carefree** | 0.8 | 1.3 | 0.6 | 0.7 | 0.6 |
| **Industrious** | 1.5 | 0.8 | 1.3 | 0.9 | 1.0 |
| **Lazy** | 0.5 | 0.7 | 0.4 | 0.6 | 0.3 |

### Effect Examples

- **Ambitious**: Works longer shifts, gains skills faster, saves more, demands higher salary. Tolerates lower needs before taking breaks.
- **Carefree**: Works shorter shifts, spends aggressively, saves little, takes breaks at higher need levels. Low salary expectations.
- **Frugal**: Normal work ethic, very low spending, high savings rate. Survives retirement easily. May die with large unspent savings (creates big inheritances).
- **Industrious**: High work ethic, moderate spending, good savings. Solid, reliable worker — the backbone of the economy.
- **Lazy**: Low work ethic, low spending, low savings. Struggles to maintain employment. Often becomes homeless.

## Archetype Assignment

At population generation, assign archetypes with weighted distribution:

```csharp
static readonly (Archetype type, float weight)[] ArchetypeWeights = {
    (Archetype.Content,      0.30f),
    (Archetype.Industrious,  0.20f),
    (Archetype.Ambitious,    0.15f),
    (Archetype.Frugal,       0.15f),
    (Archetype.Carefree,     0.10f),
    (Archetype.Lazy,         0.10f),
};
```

Most Genes fall in the Content/Industrious middle. Extreme archetypes (Lazy, Ambitious) are rarer.

## Integration Points

| System | Archetype Usage |
|---|---|
| `DecisionSystem` | Multiply critical threshold by `NeedsAwareness` |
| `WorkSystem` | Multiply shift duration by `WorkEthic` |
| `JobSkillSystem` | Multiply skill gain rate by `WorkEthic` |
| `ConsumerSystem` | Multiply spending by `SpendingRate` |
| `BankingSystem` | Multiply saving buffer by `SavingRate` |
| `HiringSystem` (JobSeeker) | Multiply `DesiredSalary` by `JobAmbition` |

All archetype effects are multiplicative on base values — the system works without archetypes (default multiplicand = 1.0), and archetypes just add variance.

## Acceptance Criteria

- Every Gene spawns with a `GeneArchetype` component
- Archetype distribution matches configured weights (±5%)
- Each archetype-using system multiplies relevant values by archetype modifier
- A Gene with no archetype (or a default Content archetype) behaves identically to current behavior
- Lazy Genes are observably worse at maintaining employment than Industrious ones
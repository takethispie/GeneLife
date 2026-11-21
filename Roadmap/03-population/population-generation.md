# Population Generation

V1 needs a way to seed the world with a diverse population of Genes across all life stages. This replaces ad-hoc entity creation with a structured population generator that produces a realistic demographic spread.

## Population Generator (`PopulationGenerator`)

A standalone class (not an ECS system — runs once at world initialization) that creates the initial Gene population.

### Entry Point

```csharp
// Called from SimulationEngine or SimulationManager during world setup
PopulationGenerator.Generate(world, config, companies);
```

### Configuration

```csharp
struct PopulationConfig {
    int TotalGenes;              // Default: 1000
    int TargetCompanies;         // Default: 5
    DistributionProfile Profile; // Age distribution
}

struct DistributionProfile {
    float ChildRatio;     // Ages 0-5   (default: 0.10)
    float StudentRatio;   // Ages 6-17  (default: 0.15)
    float WorkerRatio;    // Ages 18-64 (default: 0.55)
    float RetiredRatio;   // Ages 65-79 (default: 0.15)
    float ElderRatio;     // Ages 80+   (default: 0.05)
    // Must sum to 1.0
}
```

Defaults represent a stable population pyramid — slightly bottom-heavy, bulk in working age, tapering at older ages.

### Generation Algorithm

```
1. Create N companies (TargetCompanies)
   - Each company gets CompanyName, CompanyWallet (10000f), Office (capacity = TotalGenes / TargetCompanies / 4)
   - Each company creates M initial JobPostings (M = TotalGenes * WorkerRatio / TargetCompanies / 2)

2. For each Gene slot in TotalGenes:
   a. Pick age from distribution profile (weighted random by stage ratio, uniform within stage band)
   b. Create entity with: GenName, Alive(age), LifeStage, NetworkId, Location(random)
   c. Set LifeStage based on age
   d. Initialize stage-specific components:
      - Child (0-5):     Needs (full), Wallet(0)            [no economic activity]
      - Student (6-17):  Needs (full), Wallet(50), SkillSet (empty)
      - Worker (18-64):  Needs (full), Wallet(500), SkillSet (random), JobSeeker
      - Retired (65-79): Needs (full), Wallet(3000-ish), SkillSet (random), Savings(some), Housing
      - Elder (80+):     Needs (partial), Wallet(500), Savings(variable), Housing
   e. For Workers: attach JobSeeker and let HiringSystem match them to companies naturally
```

### Initial Wealth Distribution

Workers start with 500f, but wealth should be random within a range:

```csharp
float initialWallet = stage switch {
    LifeStage.Student => 50f + RandomVariance(20f),          // 30-70
    LifeStage.Worker  => 500f + RandomVariance(300f),        // 200-800
    LifeStage.Retired => 2000f + RandomVariance(1000f),      // 1000-3000
    LifeStage.Elder   => 200f + RandomVariance(200f),        // 0-400
    _ => 0f
};
```

Savings for Retired and Elder Genes are computed based on their age — a rough approximation of a lifetime of work:

```csharp
float estimatedLifetimeEarnings = (alive.Age - 18) * 12 * 1000f; // 1000f/month for working years
float savingsAtRetirement = estimatedLifetimeEarnings * 0.2f;     // 20% savings rate
float currentSavings = savingsAtRetirement - ((alive.Age - 65) * 12 * 500f); // Minus retirement drawdown
```

### Name Generation

Basic random name assignment from a pool for V1:

```csharp
static readonly string[] FirstNames = { "Alex", "Jordan", "Morgan", "Casey", "Riley", "Taylor", "Quinn", "Avery" };
static readonly string[] LastNames = { "Smith", "Chen", "Patel", "Kim", "Garcia", "Mueller", "Dubois", "Tanaka" };

string RandomName() => $"{Pick(FirstNames)} {Pick(LastNames)}";
```

V2 can expand this to culture-appropriate name sets.

## Acceptance Criteria

- `PopulationGenerator.Generate()` creates exactly `TotalGenes` Gene entities
- Age distribution matches configured profile within ±5% tolerance
- Each Gene has correct components for their life stage
- Workers are immediately eligible for hiring via existing `HiringSystem`
- Companies are created with sufficient capacity for the worker population
- No Gene is created with impossible state (e.g., Worker with age 10)
- Generator is deterministic with a given seed (for reproducible scenarios)